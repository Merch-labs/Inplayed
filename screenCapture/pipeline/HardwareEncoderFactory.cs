public static class HardwareEncoderFactory
{
	public static IHardwareEncoder Create(RecordingSettings settings)
	{
		_ = settings;
		return new CpuReadbackHardwareEncoder();
	}
}
