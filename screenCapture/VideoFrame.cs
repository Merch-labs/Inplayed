public sealed class VideoFrame
{
	public byte[] Data { get; }
	public int Width { get; }
	public int Height { get; }
	public long Timestamp { get; }

	public VideoFrame(byte[] data, int width, int height, long timestamp)
	{
		Data = data;
		Width = width;
		Height = height;
		Timestamp = timestamp;
	}
}
