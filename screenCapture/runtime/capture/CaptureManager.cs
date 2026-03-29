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
	private byte[] _sourcePreviewRowBuffer = Array.Empty<byte>();
	private byte[] _previewRowBuffer = Array.Empty<byte>();
	private int _previewEnabled;

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

	public void SetPreviewEnabled(bool enabled)
	{
		Interlocked.Exchange(ref _previewEnabled, enabled ? 1 : 0);
	}

	public void Dispose()
	{
		StopAsync().GetAwaiter().GetResult();
		DisposeResources(disposeEncoder: true);
	}

	public void DisposeResources(bool disposeEncoder)
	{
		_previewStaging?.Dispose();
		_previewStaging = null;
		_source.Dispose();
		if (disposeEncoder)
		{
			_encoder.Dispose();
		}
	}

	private void TryEmitPreview(TextureFrameRef frame)
	{
		if (Interlocked.CompareExchange(ref _previewEnabled, 0, 0) == 0)
		{
			return;
		}

		var handler = PreviewFrameReady;
		if (handler == null)
		{
			return;
		}

		var now = CaptureClock.NowMilliseconds();
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
			var sourceWidth = frame.Width;
			var sourceHeight = frame.Height;
			var (previewWidth, previewHeight) = PreviewFrameSizer.GetScaledSize(sourceWidth, sourceHeight);
			var previewRowBytes = previewWidth * 4;
			var bitmap = new Bitmap(previewWidth, previewHeight, PixelFormat.Format32bppArgb);
			var rect = new Rectangle(0, 0, previewWidth, previewHeight);
			var bmpData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
			try
			{
				for (var y = 0; y < previewHeight; y++)
				{
					var sourceY = previewHeight == sourceHeight
						? y
						: (int)((long)y * sourceHeight / previewHeight);
					var src = IntPtr.Add(dataBox.DataPointer, (int)(sourceY * dataBox.RowPitch));
					var dst = IntPtr.Add(bmpData.Scan0, y * bmpData.Stride);
					CopyPreviewRow(src, sourceWidth, previewWidth, previewRowBytes);
					System.Runtime.InteropServices.Marshal.Copy(_previewRowBuffer, 0, dst, previewRowBytes);
				}
			}
			finally
			{
				bitmap.UnlockBits(bmpData);
			}

			Interlocked.Exchange(ref _lastPreviewTimestampMs, now);
			handler(bitmap);
		}
		finally
		{
			sourceContext.Unmap(_previewStaging, 0);
		}
	}

	private void EnsurePreviewStaging(int width, int height, ID3D11Texture2D sourceTexture, ID3D11Device sourceDevice)
	{
		EnsurePreviewBuffers(width, height);

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

	private void EnsurePreviewBuffers(int sourceWidth, int sourceHeight)
	{
		var sourceRowBytes = sourceWidth * 4;
		if (_sourcePreviewRowBuffer.Length < sourceRowBytes)
		{
			_sourcePreviewRowBuffer = GC.AllocateUninitializedArray<byte>(sourceRowBytes);
		}

		var (previewWidth, _) = PreviewFrameSizer.GetScaledSize(sourceWidth, sourceHeight);
		var previewRowBytes = previewWidth * 4;
		if (_previewRowBuffer.Length < previewRowBytes)
		{
			_previewRowBuffer = GC.AllocateUninitializedArray<byte>(previewRowBytes);
		}
	}

	private void CopyPreviewRow(IntPtr sourceRow, int sourceWidth, int previewWidth, int previewRowBytes)
	{
		if (previewWidth == sourceWidth)
		{
			System.Runtime.InteropServices.Marshal.Copy(sourceRow, _previewRowBuffer, 0, previewRowBytes);
			ForceOpaqueAlpha(_previewRowBuffer, previewWidth);
			return;
		}

		System.Runtime.InteropServices.Marshal.Copy(sourceRow, _sourcePreviewRowBuffer, 0, sourceWidth * 4);
		for (var x = 0; x < previewWidth; x++)
		{
			var sourceX = (int)((long)x * sourceWidth / previewWidth);
			Buffer.BlockCopy(_sourcePreviewRowBuffer, sourceX * 4, _previewRowBuffer, x * 4, 4);
		}

		ForceOpaqueAlpha(_previewRowBuffer, previewWidth);
	}

	private static void ForceOpaqueAlpha(byte[] rowBuffer, int width)
	{
		for (var x = 0; x < width; x++)
		{
			rowBuffer[(x * 4) + 3] = 255;
		}
	}
}
