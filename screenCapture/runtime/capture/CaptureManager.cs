using System.Threading.Channels;
using System.Threading;
using System.Drawing;
using System.Drawing.Imaging;
using Vortice.Direct3D11;

public sealed class CaptureManager : IDisposable
{
	private readonly ICaptureSource _source;
	private readonly IHardwareEncoder _encoder;
	private readonly Channel<TextureFrameRef> _frameQueue;
	private readonly object _gate = new();

	private CancellationTokenSource? _cts;
	private Task? _encodeTask;
	private bool _running;
	private long _enqueuedFrames;
	private long _encodedFrames;
	private long _droppedFrames;
	private ID3D11Texture2D? _previewStaging;
	private int _previewWidth;
	private int _previewHeight;
	private nint _previewDevicePtr;
	private long _lastPreviewTimestampMs;
	private const long PreviewIntervalMs = 33;

	public event Action<Bitmap>? PreviewFrameReady;

	public CaptureManager(ICaptureSource source, IHardwareEncoder encoder, int queueSize = 4)
	{
		_source = source;
		_encoder = encoder;
		_frameQueue = Channel.CreateBounded<TextureFrameRef>(new BoundedChannelOptions(Math.Max(1, queueSize))
		{
			SingleReader = true,
			SingleWriter = false,
			FullMode = BoundedChannelFullMode.DropOldest
		});
	}

	public async Task StartAsync(RecordingSettings settings, CancellationToken token = default)
	{
		lock (_gate)
		{
			if (_running)
			{
				return;
			}

			_running = true;
			_cts = CancellationTokenSource.CreateLinkedTokenSource(token);
			_source.FrameArrived += OnFrameArrived;
			_source.ResolutionChanged += OnResolutionChanged;
		}

		_encoder.Start(settings);
		_encodeTask = Task.Run(() => EncodeLoop(_cts!.Token), _cts!.Token);
		await _source.StartAsync(_cts.Token);
	}

	public async Task StopAsync()
	{
		Task? encodeTask;
		CancellationTokenSource? cts;

		lock (_gate)
		{
			if (!_running)
			{
				return;
			}

			_running = false;
			_source.FrameArrived -= OnFrameArrived;
			_source.ResolutionChanged -= OnResolutionChanged;
			cts = _cts;
			encodeTask = _encodeTask;
			_cts = null;
			_encodeTask = null;
		}

		await _source.StopAsync();
		_frameQueue.Writer.TryComplete();

		if (encodeTask != null)
		{
			try
			{
				await encodeTask;
			}
			catch (OperationCanceledException)
			{
				// shutdown path
			}
		}

		_encoder.Stop();
		cts?.Cancel();
		cts?.Dispose();
	}

	private void OnFrameArrived(TextureFrameRef frame)
	{
		if (_frameQueue.Writer.TryWrite(frame))
		{
			Interlocked.Increment(ref _enqueuedFrames);
		}
		else
		{
			Interlocked.Increment(ref _droppedFrames);
			frame.Dispose();
		}
	}

	private void OnResolutionChanged(int width, int height)
	{
		_encoder.Reconfigure(width, height);
	}

	private async Task EncodeLoop(CancellationToken token)
	{
		try
		{
			await foreach (var frame in _frameQueue.Reader.ReadAllAsync(token))
			{
				using (frame)
				{
					try
					{
						TryEmitPreview(frame);
						_encoder.Encode(frame);
						Interlocked.Increment(ref _encodedFrames);
					}
					catch
					{
						Interlocked.Increment(ref _droppedFrames);
					}
				}
			}
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			// shutdown path
		}
	}

	public (long encodedFrames, long droppedFrames) GetStats()
	{
		return (
			Interlocked.Read(ref _encodedFrames),
			Interlocked.Read(ref _droppedFrames));
	}

	public (long enqueuedFrames, long encodedFrames, long droppedFrames, long pendingFrames) GetExtendedStats()
	{
		var enqueued = Interlocked.Read(ref _enqueuedFrames);
		var encoded = Interlocked.Read(ref _encodedFrames);
		var dropped = Interlocked.Read(ref _droppedFrames);
		var pending = Math.Max(0, enqueued - encoded);
		return (enqueued, encoded, dropped, pending);
	}

	public void Dispose()
	{
		StopAsync().GetAwaiter().GetResult();
		_previewStaging?.Dispose();
		_previewStaging = null;
		_source.Dispose();
		_encoder.Dispose();
	}

	private void TryEmitPreview(TextureFrameRef frame)
	{
		var now = Environment.TickCount64;
		if (now - Interlocked.Read(ref _lastPreviewTimestampMs) < PreviewIntervalMs)
		{
			return;
		}

		using var sourceDevice = frame.Texture.Device;
		if (sourceDevice == null)
		{
			return;
		}

		using var sourceContext = sourceDevice.ImmediateContext;
		if (sourceContext == null)
		{
			return;
		}

		EnsurePreviewStaging(frame.Width, frame.Height, frame.Texture, sourceDevice);
		if (_previewStaging == null)
		{
			return;
		}

		sourceContext.CopyResource(_previewStaging, frame.Texture);
		var dataBox = sourceContext.Map(_previewStaging, 0, MapMode.Read, MapFlags.None);
		try
		{
			var width = frame.Width;
			var height = frame.Height;
			var rowBytes = width * 4;
			var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
			var rect = new Rectangle(0, 0, width, height);
			var bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			try
			{
				for (var y = 0; y < height; y++)
				{
					var src = IntPtr.Add(dataBox.DataPointer, (int)(y * dataBox.RowPitch));
					var dst = IntPtr.Add(bmpData.Scan0, y * bmpData.Stride);
					System.Runtime.InteropServices.Marshal.Copy(src, _copyBuffer, 0, rowBytes);
					System.Runtime.InteropServices.Marshal.Copy(_copyBuffer, 0, dst, rowBytes);
				}
			}
			finally
			{
				bitmap.UnlockBits(bmpData);
			}

			Interlocked.Exchange(ref _lastPreviewTimestampMs, now);
			var handler = PreviewFrameReady;
			if (handler != null)
			{
				handler(bitmap);
			}
			else
			{
				bitmap.Dispose();
			}
		}
		finally
		{
			sourceContext.Unmap(_previewStaging, 0);
		}
	}

	private byte[] _copyBuffer = Array.Empty<byte>();

	private void EnsurePreviewStaging(int width, int height, ID3D11Texture2D sourceTexture, ID3D11Device sourceDevice)
	{
		var neededBytes = width * 4;
		if (_copyBuffer.Length < neededBytes)
		{
			_copyBuffer = new byte[neededBytes];
		}

		if (_previewStaging != null &&
			width == _previewWidth &&
			height == _previewHeight &&
			_previewDevicePtr == sourceDevice.NativePointer)
		{
			return;
		}

		_previewStaging?.Dispose();
		_previewStaging = null;
		var desc = sourceTexture.Description;
		desc.Usage = ResourceUsage.Staging;
		desc.BindFlags = BindFlags.None;
		desc.CPUAccessFlags = CpuAccessFlags.Read;
		desc.MiscFlags = ResourceOptionFlags.None;
		desc.MipLevels = 1;
		desc.ArraySize = 1;

		_previewStaging = sourceDevice.CreateTexture2D(desc);
		_previewWidth = width;
		_previewHeight = height;
		_previewDevicePtr = sourceDevice.NativePointer;
	}
}
