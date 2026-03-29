using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class ActiveTargetResolver
{
	public static int GetPrimaryMonitorIndex()
	{
		var primary = Screen.PrimaryScreen;
		var screens = Screen.AllScreens;
		if (primary == null)
		{
			return 0;
		}

		for (var i = 0; i < screens.Length; i++)
		{
			if (screens[i].DeviceName == primary.DeviceName)
			{
				return i;
			}
		}

		return 0;
	}

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
			}
			finally
			{
				process.Dispose();
			}
		}

		return IntPtr.Zero;
	}

	public static Rectangle GetWindowBounds(IntPtr hwnd)
	{
		if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect))
		{
			return Rectangle.Empty;
		}

		var width = rect.Right - rect.Left;
		var height = rect.Bottom - rect.Top;
		if (width <= 0 || height <= 0)
		{
			return Rectangle.Empty;
		}

		return new Rectangle(rect.Left, rect.Top, width, height);
	}

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}
}
