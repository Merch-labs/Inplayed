public static class CaptureSourceFactory
{
	public static ICaptureSource Create(RecordingSettings settings)
	{
		if (settings.Target is WindowTarget windowTarget && windowTarget.Hwnd != IntPtr.Zero)
		{
			var item = GraphicsCaptureItemFactory.CreateForWindow(windowTarget.Hwnd);
			return new WgcCaptureSource(item);
		}

		if (settings.Target is MonitorTarget monitorTarget)
		{
			var monitorHandle = GraphicsCaptureItemFactory.GetMonitorHandle(monitorTarget.MonitorIndex);
			var item = GraphicsCaptureItemFactory.CreateForMonitor(monitorHandle);
			return new WgcCaptureSource(item);
		}

		var defaultMonitor = GraphicsCaptureItemFactory.GetMonitorHandle(0);
		var defaultItem = GraphicsCaptureItemFactory.CreateForMonitor(defaultMonitor);
		return new WgcCaptureSource(defaultItem);
	}
}
