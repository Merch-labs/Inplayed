using System.Threading.Channels;

public sealed class ClipSession : IDisposable
{
	public RecordingSettings Settings { get; private set; }

	private CancellationTokenSource _cts;

	private Task _captureTask;
	private Task _encodeTask;

	private Channel<VideoFrame> _frameChannel;

	private IVideoCapture _capture;
	private IVideoEncoder _encoder;

	public event Action<string>? StatusChanged;

	public ClipSession(RecordingSettings settings)
	{
		Settings = settings;

		_frameChannel = Channel.CreateBounded<VideoFrame>(500);
	}

	public Task StartAsync()
	{
		_cts = new CancellationTokenSource();

		_capture = CaptureFactory.Create(Settings);
		_encoder = new FfmpegEncoder(Settings);

		_captureTask = Task.Run(CaptureLoop);
		_encodeTask = Task.Run(EncodeLoop);

		StatusChanged?.Invoke("running");

		return Task.CompletedTask;
	}

	public async Task StopAsync()
	{
		_cts.Cancel();

		await Task.WhenAll(_captureTask, _encodeTask);

		_capture.Dispose();
		_encoder.Dispose();

		StatusChanged?.Invoke("stopped");
	}

	private async Task CaptureLoop()
	{
		var fps = Math.Max(1, Settings.Fps);
		var frameDuration = TimeSpan.FromSeconds(1.0 / fps);
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();
		var nextFrameTime = stopwatch.Elapsed;

		while (!_cts.IsCancellationRequested)
		{
			var now = stopwatch.Elapsed;
			if (now < nextFrameTime)
			{
				var delay = nextFrameTime - now;
				if (delay > TimeSpan.Zero)
				{
					await Task.Delay(delay);
				}
			}

			var frame = _capture.CaptureFrame();
			await _frameChannel.Writer.WriteAsync(frame);

			nextFrameTime += frameDuration;
			var drift = stopwatch.Elapsed - nextFrameTime;
			if (drift > frameDuration)
			{
				nextFrameTime = stopwatch.Elapsed + frameDuration;
			}
		}
	}

	private async Task EncodeLoop()
	{
		await foreach (var frame in _frameChannel.Reader.ReadAllAsync())
		{
			_encoder.PushFrame(frame);
		}
	}

	public Task SaveClipAsync(string path)
	{
		return _encoder.FlushRecentAsync(
			path,
			TimeSpan.FromSeconds(Settings.ClipSeconds));
	}

	public void Dispose()
	{
		_cts?.Cancel();
	}
}
