public sealed class ClipSession : IDisposable
{
	public RecordingSettings Settings { get; private set; }

	private CancellationTokenSource? _cts;
	private ICaptureSource? _captureSource;
	private IHardwareEncoder? _encoder;
	private CaptureManager? _captureManager;

	public event Action<string>? StatusChanged;

	public ClipSession(RecordingSettings settings)
	{
		Settings = settings;
	}

	public async Task StartAsync()
	{
		if (_captureManager != null)
		{
			return;
		}

		_cts = new CancellationTokenSource();
		_captureSource = CaptureSourceFactory.Create(Settings);
		_encoder = HardwareEncoderFactory.Create(Settings);
		_captureManager = new CaptureManager(_captureSource, _encoder);

		await _captureManager.StartAsync(Settings, _cts.Token);

		StatusChanged?.Invoke($"encoder:{_encoder.BackendName}");
		StatusChanged?.Invoke("running");
	}

	public async Task StopAsync()
	{
		_cts?.Cancel();

		if (_captureManager != null)
		{
			await _captureManager.StopAsync();
			_captureManager.Dispose();
			_captureManager = null;
		}
		else
		{
			_captureSource?.Dispose();
			_encoder?.Dispose();
		}

		_captureSource = null;
		_encoder = null;
		_cts?.Dispose();
		_cts = null;

		StatusChanged?.Invoke("stopped");
	}

	public Task SaveClipAsync(string path)
	{
		if (_encoder == null)
		{
			return Task.CompletedTask;
		}

		return _encoder.FlushRecentAsync(
			path,
			TimeSpan.FromSeconds(Settings.ClipSeconds),
			CancellationToken.None);
	}

	public void Dispose()
	{
		StopAsync().GetAwaiter().GetResult();
	}
}
