using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class ActiveTargetResolver
{
	public static int GetActiveMonitorIndex()
	{
		var hwnd = GetForegroundWindow();
		if (hwnd == IntPtr.Zero)
		{
			return 0;
		}

		var screen = Screen.FromHandle(hwnd);
		var screens = Screen.AllScreens;
		for (var i = 0; i < screens.Length; i++)
		{
			if (screens[i].DeviceName == screen.DeviceName)
			{
				return i;
			}
		}

		return 0;
	}

	public static IntPtr GetActiveWindowHandle()
	{
		return GetForegroundWindow();
	}

	public static IntPtr GetWindowHandleByExecutablePath(string executablePath)
	{
		if (string.IsNullOrWhiteSpace(executablePath))
		{
			return IntPtr.Zero;
		}

		var fullPath = Path.GetFullPath(executablePath);
		foreach (var process in Process.GetProcesses())
		{
			try
			{
				if (process.MainWindowHandle == IntPtr.Zero)
				{
					continue;
				}

				var modulePath = process.MainModule?.FileName;
				if (modulePath == null)
				{
					continue;
				}

				if (string.Equals(Path.GetFullPath(modulePath), fullPath, StringComparison.OrdinalIgnoreCase))
				{
					return process.MainWindowHandle;
				}
			}
			catch
			{
				// ignore access denied/system processes
			}
			finally
			{
				process.Dispose();
			}
		}

		return IntPtr.Zero;
	}

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();
}
