public sealed class RecordingSettings
{
	public int Width { get; set; }
	public int Height { get; set; }
	public int Fps { get; set; }
	public int Bitrate { get; set; }
	public int ClipSeconds { get; set; }

	public CaptureTarget Target { get; set; } = new MonitorTarget { MonitorIndex = 0 };
}
