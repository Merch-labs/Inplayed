using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using Vortice.Direct3D11;

public sealed class FfmpegPacketRingHardwareEncoder : IHardwareEncoder
{
	public string BackendName => $"FfmpegPacketRing:{_videoCodec}";

	private readonly object _gate = new();
	private readonly string _videoCodec;
	private readonly H264AnnexBPacketizer _packetizer = new();
	private readonly IClipWriter _clipWriter = new FfmpegClipWriter();

	private RecordingSettings? _settings;
	private ID3D11Texture2D? _staging;
	private int _stagingWidth;
	private int _stagingHeight;
	private nint _stagingDevicePtr;
	private int _inputWidth;
	private int _inputHeight;
	private bool _restartRequested;

	private Process? _ffmpeg;
	private Stream? _stdin;
	private Channel<PendingInput>? _inputQueue;
	private Task? _stdinTask;
	private Task? _stdoutTask;
	private Task? _stderrTask;
	private EncodedPacketRingBuffer? _ringBuffer;
	private bool _running;
	private Stopwatch _clock = new();
	private long _inputBytes;
	private long _packetBytes;
	private long _packetCount;
	private long _restartCount;
	private long _queueDrops;
	private long _queuedBuffers;
	private long _lastSubmittedFrameTs;
	private long _gpuCopyFailures;
	private double _frameIntervalMs = 16.6667;
	private long _duplicatedFrames;
	private long _stderrCharsRead;
	private readonly Stack<byte[]> _bufferPool = new();
	private readonly object _bufferPoolGate = new();
	private int _bufferSizeBytes;
	private int _maxPooledBuffers = 4;

	public FfmpegPacketRingHardwareEncoder(string videoCodec)
	{
		_videoCodec = string.IsNullOrWhiteSpace(videoCodec) ? "libx264" : videoCodec;
	}

	public void Start(RecordingSettings settings)
	{
		lock (_gate)
		{
			if (_running)
			{
				return;
			}

			_settings = settings;
			_inputWidth = Math.Max(1, settings.Width);
			_inputHeight = Math.Max(1, settings.Height);
			_frameIntervalMs = 1000.0 / Math.Max(1, settings.Fps);
			lock (_bufferPoolGate)
			{
				_bufferSizeBytes = 0;
				_bufferPool.Clear();
			}
			_ringBuffer = new EncodedPacketRingBuffer(TimeSpan.FromSeconds(Math.Max(1, settings.ClipSeconds) + 4));
			_clock = Stopwatch.StartNew();
			StartFfmpegLocked(settings, _videoCodec, _inputWidth, _inputHeight);
			_running = true;
		}
	}

	public void Encode(TextureFrameRef frame)
	{
		lock (_gate)
		{
			if (!_running || _settings == null || _stdin == null)
			{
				return;
			}

			using var sourceDevice = frame.Texture.Device;
			if (sourceDevice == null)
			{
				Interlocked.Increment(ref _gpuCopyFailures);
				return;
			}

			using var sourceContext = sourceDevice.ImmediateContext;
			if (sourceContext == null)
			{
				Interlocked.Increment(ref _gpuCopyFailures);
				return;
			}

			EnsureStaging(frame.Width, frame.Height, frame.Texture, sourceDevice);
			if (_staging == null)
			{
				Interlocked.Increment(ref _gpuCopyFailures);
				return;
			}

			if (_restartRequested || frame.Width != _inputWidth || frame.Height != _inputHeight)
			{
				var nextWidth = frame.Width;
				var nextHeight = frame.Height;
				if (_restartRequested)
				{
					nextWidth = _inputWidth;
					nextHeight = _inputHeight;
				}

				RestartFfmpegLocked(nextWidth, nextHeight);
				_restartRequested = false;
			}

			sourceContext.CopyResource(_staging, frame.Texture);
			var dataBox = sourceContext.Map(_staging, 0, MapMode.Read, MapFlags.None);
			try
			{
				var width = frame.Width;
				var height = frame.Height;
				var rowBytes = width * 4;
				var totalBytes = rowBytes * height;
				var buffer = RentFrameBuffer(totalBytes);
				try
				{
					for (var y = 0; y < height; y++)
					{
						var src = IntPtr.Add(dataBox.DataPointer, (int)(y * dataBox.RowPitch));
						Marshal.Copy(src, buffer, y * rowBytes, rowBytes);
					}

					if (_inputQueue == null || !_inputQueue.Writer.TryWrite(new PendingInput(buffer, totalBytes, frame.Timestamp)))
					{
						Interlocked.Increment(ref _queueDrops);
						ReturnFrameBuffer(buffer);
						return;
					}

					Interlocked.Increment(ref _queuedBuffers);
				}
				finally
				{
					// buffer returned by input writer or on queue drop path
				}
			}
			catch (IOException)
			{
				_running = false;
			}
			finally
			{
				sourceContext.Unmap(_staging, 0);
			}
		}
	}

	public void Reconfigure(int width, int height)
	{
		lock (_gate)
		{
			_inputWidth = Math.Max(1, width);
			_inputHeight = Math.Max(1, height);
			_restartRequested = true;
		}
	}

	public Task FlushRecentAsync(string outputPath, TimeSpan clipLength, CancellationToken token = default)
	{
		EncodedPacketSnapshot snapshot;
		lock (_gate)
		{
			if (_ringBuffer == null)
			{
				return Task.CompletedTask;
			}

			snapshot = _ringBuffer.SnapshotLast(clipLength);
		}

		return _clipWriter.WriteAsync(outputPath, snapshot, clipLength, token);
	}

	public string GetDebugStatus()
	{
		lock (_gate)
		{
			var ring = _ringBuffer?.GetStats() ?? (0, 0L);
			var ffmpegWorkingSet = _ffmpeg?.WorkingSet64 ?? 0;
			return $"codec={_videoCodec};running={_running};inputBytes={Interlocked.Read(ref _inputBytes)};packets={Interlocked.Read(ref _packetCount)};packetBytes={Interlocked.Read(ref _packetBytes)};ringPackets={ring.Item1};ringBytes={ring.Item2};restarts={Interlocked.Read(ref _restartCount)};queueDrops={Interlocked.Read(ref _queueDrops)};queuedBuffers={Interlocked.Read(ref _queuedBuffers)};lastFrameTs={Interlocked.Read(ref _lastSubmittedFrameTs)};gpuCopyFailures={Interlocked.Read(ref _gpuCopyFailures)};duplicatedFrames={Interlocked.Read(ref _duplicatedFrames)};stderrChars={Interlocked.Read(ref _stderrCharsRead)};ffmpegWorkingSet={ffmpegWorkingSet}";
		}
	}

	public void Stop()
	{
		lock (_gate)
		{
			if (!_running)
			{
				return;
			}

			_running = false;
			StopFfmpegProcessLocked();
		}
	}

	public void Dispose()
	{
		Stop();

		lock (_gate)
		{
			_staging?.Dispose();
			_staging = null;
			_stagingDevicePtr = 0;
			_ffmpeg?.Dispose();
			_ffmpeg = null;
		}
	}

	private void StartFfmpegLocked(RecordingSettings settings, string codec, int width, int height)
	{
		var fps = Math.Max(1, settings.Fps);
		var bitrate = Math.Max(1, settings.Bitrate);
		var ffmpegPath = ResolveFfmpegPath();
		var codecArgs = BuildCodecArgs(codec, fps, bitrate);
		var args =
			$"-hide_banner -loglevel error -y -f rawvideo -pix_fmt bgra -s {width}x{height} -r {fps} " +
			$"-i pipe:0 -an {codecArgs} -f h264 pipe:1";

		var psi = new ProcessStartInfo
		{
			FileName = ffmpegPath,
			Arguments = args,
			UseShellExecute = false,
			RedirectStandardInput = true,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		try
		{
			_ffmpeg = Process.Start(psi);
			if (_ffmpeg == null)
			{
				throw new InvalidOperationException("Failed to start ffmpeg process.");
			}

			_stdin = _ffmpeg.StandardInput.BaseStream;
			_inputQueue = Channel.CreateBounded<PendingInput>(new BoundedChannelOptions(8)
			{
				SingleReader = true,
				SingleWriter = false,
				FullMode = BoundedChannelFullMode.DropWrite
			});
			_stdinTask = Task.Run(WriteInputLoop);
			_stdoutTask = Task.Run(ReadEncodedStdoutLoop);
			_stderrTask = Task.Run(ReadStderrLoop);
		}
		catch (Win32Exception)
		{
			throw new InvalidOperationException(
				"ffmpeg was not found. Place ffmpeg.exe next to the app, in tools\\ffmpeg\\ffmpeg.exe, or install ffmpeg on PATH.");
		}
	}

	private void RestartFfmpegLocked(int width, int height)
	{
		StopFfmpegProcessLocked();
		if (_settings == null)
		{
			return;
		}

		_inputWidth = Math.Max(1, width);
		_inputHeight = Math.Max(1, height);
		Interlocked.Increment(ref _restartCount);
		StartFfmpegLocked(_settings, _videoCodec, _inputWidth, _inputHeight);
	}

	private void StopFfmpegProcessLocked()
	{
		var ffmpeg = _ffmpeg;
		var inputQueue = _inputQueue;
		var stdinTask = _stdinTask;
		var stdoutTask = _stdoutTask;
		var stderrTask = _stderrTask;
		_ffmpeg = null;
		_inputQueue = null;
		_stdinTask = null;
		_stdoutTask = null;
		_stderrTask = null;
		inputQueue?.Writer.TryComplete();

		try
		{
			stdinTask?.Wait(750);
		}
		catch
		{
		}

		try
		{
			_stdin?.Flush();
		}
		catch
		{
		}

		try
		{
			_stdin?.Dispose();
		}
		catch
		{
		}

		_stdin = null;

		if (ffmpeg != null)
		{
			try
			{
				ffmpeg.WaitForExit(2000);
			}
			catch
			{
			}
		}

		try
		{
			stdoutTask?.Wait(500);
			stderrTask?.Wait(500);
		}
		catch
		{
		}

		ffmpeg?.Dispose();
	}

	private static string BuildCodecArgs(string codec, int fps, int bitrate)
	{
		var keyint = Math.Max(1, fps);
		if (codec.Contains("nvenc", StringComparison.OrdinalIgnoreCase))
		{
			return $"-c:v {codec} -preset p1 -tune ll -g {keyint} -bf 0 -b:v {bitrate}";
		}

		if (codec.Equals("libx264", StringComparison.OrdinalIgnoreCase))
		{
			return $"-c:v libx264 -preset veryfast -tune zerolatency -g {keyint} -bf 0 -b:v {bitrate}";
		}

		return $"-c:v {codec} -g {keyint} -bf 0 -b:v {bitrate}";
	}

	private async Task WriteInputLoop()
	{
		var queue = _inputQueue;
		var stdin = _stdin;
		if (queue == null || stdin == null)
		{
			return;
		}

		try
		{
			await foreach (var input in queue.Reader.ReadAllAsync())
			{
				try
				{
					var ts = input.FrameTimestamp;
					await stdin.WriteAsync(input.Buffer, 0, input.Count);
					Interlocked.Add(ref _inputBytes, input.Count);
					Interlocked.Exchange(ref _lastSubmittedFrameTs, ts);
				}
				finally
				{
					Interlocked.Decrement(ref _queuedBuffers);
					ReturnFrameBuffer(input.Buffer);
				}
			}
		}
		catch
		{
		}
	}

	private async Task ReadEncodedStdoutLoop()
	{
		if (_ffmpeg == null || _ringBuffer == null)
		{
			return;
		}

		var stream = _ffmpeg.StandardOutput.BaseStream;
		var readBuffer = new byte[64 * 1024];

		while (true)
		{
			int bytesRead;
			try
			{
				bytesRead = await stream.ReadAsync(readBuffer, 0, readBuffer.Length);
			}
			catch
			{
				break;
			}

			if (bytesRead <= 0)
			{
				break;
			}

			var tsMs = Interlocked.Read(ref _lastSubmittedFrameTs);
			if (tsMs <= 0)
			{
				tsMs = _clock.ElapsedMilliseconds;
			}
			var packets = _packetizer.Push(readBuffer.AsSpan(0, bytesRead), tsMs, tsMs);
			for (var i = 0; i < packets.Count; i++)
			{
				_ringBuffer.Append(packets[i]);
				Interlocked.Increment(ref _packetCount);
				Interlocked.Add(ref _packetBytes, packets[i].Data.Length);
			}
		}

		var endTsMs = Interlocked.Read(ref _lastSubmittedFrameTs);
		if (endTsMs <= 0)
		{
			endTsMs = _clock.ElapsedMilliseconds;
		}
		var tail = _packetizer.Flush(endTsMs, endTsMs);
		for (var i = 0; i < tail.Count; i++)
		{
			_ringBuffer.Append(tail[i]);
			Interlocked.Increment(ref _packetCount);
			Interlocked.Add(ref _packetBytes, tail[i].Data.Length);
		}
	}

	private async Task ReadStderrLoop()
	{
		var ffmpeg = _ffmpeg;
		if (ffmpeg == null)
		{
			return;
		}

		var stderr = ffmpeg.StandardError;
		var buffer = new char[1024];
		try
		{
			while (true)
			{
				var read = await stderr.ReadAsync(buffer, 0, buffer.Length);
				if (read <= 0)
				{
					break;
				}

				Interlocked.Add(ref _stderrCharsRead, read);
			}
		}
		catch
		{
		}
	}

	private void EnsureStaging(int width, int height, ID3D11Texture2D sourceTexture, ID3D11Device sourceDevice)
	{
		if (_staging != null &&
			width == _stagingWidth &&
			height == _stagingHeight &&
			_stagingDevicePtr == sourceDevice.NativePointer)
		{
			return;
		}

		_staging?.Dispose();
		_staging = null;
		var desc = sourceTexture.Description;
		desc.Usage = ResourceUsage.Staging;
		desc.BindFlags = BindFlags.None;
		desc.CPUAccessFlags = CpuAccessFlags.Read;
		desc.MiscFlags = ResourceOptionFlags.None;
		desc.MipLevels = 1;
		desc.ArraySize = 1;

		_staging = sourceDevice.CreateTexture2D(desc);
		_stagingWidth = width;
		_stagingHeight = height;
		_stagingDevicePtr = sourceDevice.NativePointer;
	}

	private static string ResolveFfmpegPath()
	{
		var baseDir = AppContext.BaseDirectory;
		var local = Path.Combine(baseDir, "ffmpeg.exe");
		if (File.Exists(local))
		{
			return local;
		}

		var tools = Path.Combine(baseDir, "tools", "ffmpeg", "ffmpeg.exe");
		if (File.Exists(tools))
		{
			return tools;
		}

		return "ffmpeg";
	}

	private byte[] RentFrameBuffer(int size)
	{
		lock (_bufferPoolGate)
		{
			if (_bufferSizeBytes != size)
			{
				_bufferPool.Clear();
				_bufferSizeBytes = size;
			}

			if (_bufferPool.Count > 0)
			{
				return _bufferPool.Pop();
			}
		}

		return new byte[size];
	}

	private void ReturnFrameBuffer(byte[] buffer)
	{
		lock (_bufferPoolGate)
		{
			if (buffer.Length != _bufferSizeBytes)
			{
				return;
			}

			if (_bufferPool.Count >= _maxPooledBuffers)
			{
				return;
			}

			_bufferPool.Push(buffer);
		}
	}

	private readonly record struct PendingInput(byte[] Buffer, int Count, long FrameTimestamp);
}



