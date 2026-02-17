public static class CaptureSourceFactory
{
	public static ICaptureSource Create(RecordingSettings settings)
	{
		if (settings.Target is WindowTarget windowTarget && windowTarget.Hwnd != IntPtr.Zero)
		{
			var item = GraphicsCaptureItemFactory.CreateForWindow(windowTarget.Hwnd);
			return new WgcCaptureSource(item);
		}

		if (settings.Target is ProcessTarget processTarget && processTarget.ProcessId > 0)
		{
			var hwnd = TryGetMainWindowHandle(processTarget.ProcessId);
			if (hwnd != IntPtr.Zero)
			{
				var item = GraphicsCaptureItemFactory.CreateForWindow(hwnd);
				return new WgcCaptureSource(item);
			}
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

	private static IntPtr TryGetMainWindowHandle(int processId)
	{
		try
		{
			using var process = System.Diagnostics.Process.GetProcessById(processId);
			var hwnd = process.MainWindowHandle;
			return hwnd;
		}
		catch
		{
			return IntPtr.Zero;
		}
	}
}
