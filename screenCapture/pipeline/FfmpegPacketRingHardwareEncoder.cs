using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Vortice.Direct3D11;

public sealed class FfmpegPacketRingHardwareEncoder : IHardwareEncoder
{
	private readonly object _gate = new();
	private readonly string _videoCodec;
	private readonly H264AnnexBPacketizer _packetizer = new();
	private readonly IClipWriter _clipWriter = new FfmpegClipWriter();

	private RecordingSettings? _settings;
	private D3D11DeviceBundle? _device;
	private ID3D11Texture2D? _staging;
	private int _stagingWidth;
	private int _stagingHeight;
	private int _outputWidth;
	private int _outputHeight;

	private Process? _ffmpeg;
	private Stream? _stdin;
	private Task? _stdoutTask;
	private Task? _stderrTask;
	private EncodedPacketRingBuffer? _ringBuffer;
	private bool _running;

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
			_outputWidth = settings.Width;
			_outputHeight = settings.Height;
			_device = D3D11Helper.CreateDevice();
			_ringBuffer = new EncodedPacketRingBuffer(TimeSpan.FromSeconds(Math.Max(1, settings.ClipSeconds)));
			StartFfmpegLocked(settings, _videoCodec);
			_running = true;
		}
	}

	public void Encode(TextureFrameRef frame)
	{
		lock (_gate)
		{
			if (!_running || _settings == null || _device == null || _stdin == null)
			{
				return;
			}

			EnsureStaging(frame.Width, frame.Height, frame.Texture);
			if (_staging == null)
			{
				return;
			}

			_device.Context.CopyResource(_staging, frame.Texture);
			var dataBox = _device.Context.Map(_staging, 0, MapMode.Read, MapFlags.None);
			try
			{
				var width = frame.Width;
				var height = frame.Height;
				var rowBytes = width * 4;
				var buffer = new byte[rowBytes * height];
				for (var y = 0; y < height; y++)
				{
					var src = IntPtr.Add(dataBox.DataPointer, (int)(y * dataBox.RowPitch));
					Marshal.Copy(src, buffer, y * rowBytes, rowBytes);
				}

				if (width != _outputWidth || height != _outputHeight)
				{
					buffer = GdiScaler.ScaleBgra(buffer, width, height, _outputWidth, _outputHeight);
				}

				_stdin.Write(buffer, 0, buffer.Length);
			}
			catch (IOException)
			{
				_running = false;
			}
			finally
			{
				_device.Context.Unmap(_staging, 0);
			}
		}
	}

	public void Reconfigure(int width, int height)
	{
		lock (_gate)
		{
			_outputWidth = Math.Max(1, width);
			_outputHeight = Math.Max(1, height);
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

		return _clipWriter.WriteAsync(outputPath, snapshot, token);
	}

	public void Stop()
	{
		Process? ffmpeg;
		Task? stdoutTask;
		Task? stderrTask;

		lock (_gate)
		{
			if (!_running)
			{
				return;
			}

			_running = false;
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
			ffmpeg = _ffmpeg;
			_ffmpeg = null;
			stdoutTask = _stdoutTask;
			_stdoutTask = null;
			stderrTask = _stderrTask;
			_stderrTask = null;
		}

		if (ffmpeg != null)
		{
			try
			{
				ffmpeg.WaitForExit(3000);
			}
			catch
			{
			}
		}

		try
		{
			stdoutTask?.Wait(1000);
			stderrTask?.Wait(1000);
		}
		catch
		{
		}
	}

	public void Dispose()
	{
		Stop();

		lock (_gate)
		{
			_staging?.Dispose();
			_staging = null;
			_device?.Dispose();
			_device = null;
			_ffmpeg?.Dispose();
			_ffmpeg = null;
		}
	}

	private void StartFfmpegLocked(RecordingSettings settings, string codec)
	{
		var fps = Math.Max(1, settings.Fps);
		var bitrate = Math.Max(1, settings.Bitrate);
		var ffmpegPath = ResolveFfmpegPath();
		var codecArgs = BuildCodecArgs(codec, fps, bitrate);
		var args =
			$"-hide_banner -loglevel error -y -f rawvideo -pix_fmt bgra -s {settings.Width}x{settings.Height} -r {fps} " +
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
			_stdoutTask = Task.Run(ReadEncodedStdoutLoop);
			_stderrTask = Task.Run(() => _ffmpeg.StandardError.ReadToEnd());
		}
		catch (Win32Exception)
		{
			throw new InvalidOperationException(
				"ffmpeg was not found. Place ffmpeg.exe next to the app, in tools\\ffmpeg\\ffmpeg.exe, or install ffmpeg on PATH.");
		}
	}

	private static string BuildCodecArgs(string codec, int fps, int bitrate)
	{
		if (codec.Contains("nvenc", StringComparison.OrdinalIgnoreCase))
		{
			return $"-c:v {codec} -preset p1 -tune ll -g {fps * 2} -bf 0 -b:v {bitrate}";
		}

		if (codec.Equals("libx264", StringComparison.OrdinalIgnoreCase))
		{
			return $"-c:v libx264 -preset veryfast -tune zerolatency -g {fps * 2} -bf 0 -b:v {bitrate}";
		}

		return $"-c:v {codec} -g {fps * 2} -bf 0 -b:v {bitrate}";
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

			var packets = _packetizer.Push(readBuffer.AsSpan(0, bytesRead), 0, 0);
			for (var i = 0; i < packets.Count; i++)
			{
				_ringBuffer.Append(packets[i]);
			}
		}

		var tail = _packetizer.Flush(0, 0);
		for (var i = 0; i < tail.Count; i++)
		{
			_ringBuffer.Append(tail[i]);
		}
	}

	private void EnsureStaging(int width, int height, ID3D11Texture2D sourceTexture)
	{
		if (_staging != null && width == _stagingWidth && height == _stagingHeight)
		{
			return;
		}

		_staging?.Dispose();
		var desc = sourceTexture.Description;
		desc.Usage = ResourceUsage.Staging;
		desc.BindFlags = BindFlags.None;
		desc.CPUAccessFlags = CpuAccessFlags.Read;
		desc.MiscFlags = ResourceOptionFlags.None;
		desc.MipLevels = 1;
		desc.ArraySize = 1;

		_staging = _device!.Device.CreateTexture2D(desc);
		_stagingWidth = width;
		_stagingHeight = height;
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
}
