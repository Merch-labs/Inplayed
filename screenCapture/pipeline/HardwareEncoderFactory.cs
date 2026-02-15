public static class HardwareEncoderFactory
{
	public static IHardwareEncoder Create(RecordingSettings settings)
	{
		_ = settings;
		if (FfmpegCapabilities.SupportsEncoder("h264_nvenc"))
		{
			Console.WriteLine("Encoder preferred: h264_nvenc (ffmpeg packet ring)");
			return new AdaptiveHardwareEncoder(
				() => new FfmpegPacketRingHardwareEncoder("h264_nvenc"),
				() => new FfmpegPacketRingHardwareEncoder("libx264"),
				() => new CpuReadbackHardwareEncoder("libx264"));
		}

		Console.WriteLine("Encoder preferred: libx264 (ffmpeg packet ring)");
		return new AdaptiveHardwareEncoder(
			() => new FfmpegPacketRingHardwareEncoder("libx264"),
			() => new CpuReadbackHardwareEncoder("libx264"));
	}
}
