public sealed class NvencHardwareEncoder : IHardwareEncoder
{
	public string BackendName => "NvencNative:Unavailable";

	public void Start(RecordingSettings settings)
	{
		_ = settings;
		throw new NotSupportedException(
			"Native NVENC backend is not implemented yet. Use ffmpeg packet-ring backend for now.");
	}

	public void Encode(TextureFrameRef frame)
	{
		_ = frame;
	}

	public void Reconfigure(int width, int height)
	{
		_ = width;
		_ = height;
	}

	public Task FlushRecentAsync(string outputPath, TimeSpan clipLength, CancellationToken token = default)
	{
		_ = outputPath;
		_ = clipLength;
		_ = token;
		return Task.CompletedTask;
	}

	public string GetDebugStatus()
	{
		return "native_nvenc=not_implemented";
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
	}
}
