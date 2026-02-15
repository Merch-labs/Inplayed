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
		StatusChanged?.Invoke($"encoderPolicy:{HardwareEncoderFactory.GetSelectionDebug()}");
		_encoder = HardwareEncoderFactory.Create(Settings);
		_captureManager = new CaptureManager(_captureSource, _encoder);

		try
		{
			await _captureManager.StartAsync(Settings, _cts.Token);
			StatusChanged?.Invoke($"encoder:{_encoder.BackendName}");
			StatusChanged?.Invoke("running");
		}
		catch (Exception ex)
		{
			StatusChanged?.Invoke($"failed:{ex.GetType().Name}:{ex.Message}");
			try
			{
				await StopAsync();
			}
			catch
			{
			}

			throw;
		}
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

	public string GetDebugStatus()
	{
		if (_captureManager == null || _encoder == null)
		{
			return "inactive";
		}

		var (enqueued, encoded, dropped, pending) = _captureManager.GetExtendedStats();
		var encoderDebug = _encoder.GetDebugStatus();
		return $"backend={_encoder.BackendName};enqueued={enqueued};encoded={encoded};pending={pending};dropped={dropped};encoder={encoderDebug}";
	}

	public void Dispose()
	{
		StopAsync().GetAwaiter().GetResult();
	}
}
