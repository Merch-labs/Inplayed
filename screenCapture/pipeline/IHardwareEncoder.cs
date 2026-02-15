public interface IHardwareEncoder : IDisposable
{
	void Start(RecordingSettings settings);
	void Encode(TextureFrameRef frame);
	void Reconfigure(int width, int height);
	void Stop();
}
