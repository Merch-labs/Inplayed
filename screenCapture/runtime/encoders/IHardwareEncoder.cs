public interface IHardwareEncoder : IDisposable
{
	string BackendName { get; }
	string GetDebugStatus();
	void Start(RecordingSettings settings);
	void Encode(TextureFrameRef frame);
	void Reconfigure(int width, int height);
	Task FlushRecentAsync(string outputPath, TimeSpan clipLength, CancellationToken token = default);
	void Stop();
}
