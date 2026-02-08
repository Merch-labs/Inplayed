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

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();
}
