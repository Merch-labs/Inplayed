public static class HardwareEncoderFactory
{
	public static IHardwareEncoder Create(RecordingSettings settings)
	{
		_ = settings;
		if (FfmpegCapabilities.SupportsEncoder("h264_nvenc"))
		{
			Console.WriteLine("Encoder selected: h264_nvenc (ffmpeg)");
			return new CpuReadbackHardwareEncoder("h264_nvenc");
		}

		Console.WriteLine("Encoder selected: libx264 (ffmpeg)");
		return new CpuReadbackHardwareEncoder("libx264");
	}
}
