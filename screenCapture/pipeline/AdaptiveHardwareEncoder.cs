public sealed class AdaptiveHardwareEncoder : IHardwareEncoder
{
	public string BackendName => _active?.BackendName ?? "Adaptive:Uninitialized";

	private readonly Func<IHardwareEncoder>[] _candidates;
	private IHardwareEncoder? _active;

	public AdaptiveHardwareEncoder(params Func<IHardwareEncoder>[] candidates)
	{
		_candidates = candidates.Length == 0
			? new Func<IHardwareEncoder>[] { () => new CpuReadbackHardwareEncoder("libx264") }
			: candidates;
	}

	public void Start(RecordingSettings settings)
	{
		Exception? last = null;
		foreach (var create in _candidates)
		{
			IHardwareEncoder? encoder = null;
			try
			{
				encoder = create();
				encoder.Start(settings);
				_active = encoder;
				Console.WriteLine($"Encoder backend active: {encoder.BackendName}");
				return;
			}
			catch (Exception ex)
			{
				last = ex;
				try
				{
					encoder?.Dispose();
				}
				catch
				{
				}
			}
		}

		throw new InvalidOperationException("No encoder backend could be started.", last);
	}

	public void Encode(TextureFrameRef frame)
	{
		_active?.Encode(frame);
	}

	public void Reconfigure(int width, int height)
	{
		_active?.Reconfigure(width, height);
	}

	public Task FlushRecentAsync(string outputPath, TimeSpan clipLength, CancellationToken token = default)
	{
		if (_active == null)
		{
			return Task.CompletedTask;
		}

		return _active.FlushRecentAsync(outputPath, clipLength, token);
	}

	public void Stop()
	{
		_active?.Stop();
	}

	public void Dispose()
	{
		_active?.Dispose();
		_active = null;
	}
}
