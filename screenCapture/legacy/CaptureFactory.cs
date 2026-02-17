public static class CaptureFactory
{
	public static IVideoCapture Create(RecordingSettings settings)
	{
		if (settings.Target is WindowTarget windowTarget && windowTarget.Hwnd != IntPtr.Zero)
		{
			return new WindowCapture(windowTarget.Hwnd, settings.Width, settings.Height);
		}

		if (settings.Target is MonitorTarget monitorTarget)
		{
			return new MonitorCapture(monitorTarget.MonitorIndex, settings.Width, settings.Height);
		}

		return new MonitorCapture(0, settings.Width, settings.Height);
	}
}