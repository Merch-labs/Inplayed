public interface ICaptureSource : IDisposable
{
	event Action<TextureFrameRef>? FrameArrived;
	event Action<int, int>? ResolutionChanged;

	Task StartAsync(CancellationToken token);
	Task StopAsync();
}
