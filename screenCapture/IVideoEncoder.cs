public interface IVideoEncoder : IDisposable
{
	void PushFrame(VideoFrame frame);

	Task FlushRecentAsync(string path, TimeSpan clipLength);
}
