using System.Threading.Channels;

public sealed class CaptureManager : IDisposable
{
	private readonly ICaptureSource _source;
	private readonly IHardwareEncoder _encoder;
	private readonly Channel<TextureFrameRef> _frameQueue;
	private readonly object _gate = new();

	private CancellationTokenSource? _cts;
	private Task? _encodeTask;
	private bool _running;

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
		if (!_frameQueue.Writer.TryWrite(frame))
		{
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
					_encoder.Encode(frame);
				}
			}
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			// shutdown path
		}
	}

	public void Dispose()
	{
		StopAsync().GetAwaiter().GetResult();
		_source.Dispose();
		_encoder.Dispose();
	}
}
