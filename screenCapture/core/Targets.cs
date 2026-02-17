public abstract class CaptureTarget { }

public sealed class MonitorTarget : CaptureTarget
{
	public int MonitorIndex { get; set; }
}

public sealed class WindowTarget : CaptureTarget
{
	public IntPtr Hwnd { get; set; }
}

public sealed class ProcessTarget : CaptureTarget
{
	public int ProcessId { get; set; }
}
