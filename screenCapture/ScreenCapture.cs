using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public sealed class MonitorCapture : IVideoCapture
{
	private readonly int _width;
	private readonly int _height;
	private readonly Rectangle _bounds;
	private readonly Bitmap _screenBitmap;
	private readonly Bitmap _targetBitmap;

	public MonitorCapture(int monitorIndex, int width, int height)
	{
		_width = width;
		_height = height;

		var screens = Screen.AllScreens;
		var screen = (monitorIndex >= 0 && monitorIndex < screens.Length)
			? screens[monitorIndex]
			: Screen.PrimaryScreen;

		_bounds = screen?.Bounds ?? new Rectangle(0, 0, width, height);
		_screenBitmap = new Bitmap(_bounds.Width, _bounds.Height, PixelFormat.Format32bppArgb);
		_targetBitmap = new Bitmap(_width, _height, PixelFormat.Format32bppArgb);
	}

	public VideoFrame CaptureFrame()
	{
		using (var g = Graphics.FromImage(_screenBitmap))
		{
			g.CopyFromScreen(_bounds.Location, Point.Empty, _bounds.Size);
		}

		using (var g = Graphics.FromImage(_targetBitmap))
		{
			g.DrawImage(_screenBitmap, 0, 0, _width, _height);
		}

		return FrameFromBitmap(_targetBitmap);
	}

	public void Dispose()
	{
		_screenBitmap.Dispose();
		_targetBitmap.Dispose();
	}

	internal static VideoFrame FrameFromBitmap(Bitmap bitmap)
	{
		var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
		var data = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

		try
		{
			var tightStride = bitmap.Width * 4;
			var buffer = new byte[tightStride * bitmap.Height];

			if (data.Stride == tightStride)
			{
				Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
			}
			else
			{
				for (var y = 0; y < bitmap.Height; y++)
				{
					var src = data.Scan0 + (y * data.Stride);
					Marshal.Copy(src, buffer, y * tightStride, tightStride);
				}
			}

			return new VideoFrame(buffer, bitmap.Width, bitmap.Height, DateTimeOffset.Now.ToUnixTimeMilliseconds());
		}
		finally
		{
			bitmap.UnlockBits(data);
		}
	}
}

public sealed class WindowCapture : IVideoCapture
{
	private readonly IntPtr _hwnd;
	private readonly int _width;
	private readonly int _height;
	private Bitmap? _windowBitmap;
	private readonly Bitmap _targetBitmap;

	public WindowCapture(IntPtr hwnd, int width, int height)
	{
		_hwnd = hwnd;
		_width = width;
		_height = height;
		_targetBitmap = new Bitmap(_width, _height, PixelFormat.Format32bppArgb);
	}

	public VideoFrame CaptureFrame()
	{
		if (!GetWindowRect(_hwnd, out var rect))
		{
			return new VideoFrame(new byte[_width * _height * 4], _width, _height, DateTimeOffset.Now.ToUnixTimeMilliseconds());
		}

		var windowWidth = Math.Max(1, rect.Right - rect.Left);
		var windowHeight = Math.Max(1, rect.Bottom - rect.Top);

		if (_windowBitmap == null || _windowBitmap.Width != windowWidth || _windowBitmap.Height != windowHeight)
		{
			_windowBitmap?.Dispose();
			_windowBitmap = new Bitmap(windowWidth, windowHeight, PixelFormat.Format32bppArgb);
		}

		var captured = false;
		using (var g = Graphics.FromImage(_windowBitmap))
		{
			var hdc = g.GetHdc();
			try
			{
				captured = PrintWindow(_hwnd, hdc, 0);
			}
			finally
			{
				g.ReleaseHdc(hdc);
			}
		}

		if (!captured)
		{
			using (var g = Graphics.FromImage(_windowBitmap))
			{
				g.CopyFromScreen(new Point(rect.Left, rect.Top), Point.Empty, new Size(windowWidth, windowHeight));
			}
		}

		using (var g = Graphics.FromImage(_targetBitmap))
		{
			g.DrawImage(_windowBitmap, 0, 0, _width, _height);
		}

		return MonitorCapture.FrameFromBitmap(_targetBitmap);
	}

	public void Dispose()
	{
		_windowBitmap?.Dispose();
		_targetBitmap.Dispose();
	}

	[DllImport("user32.dll")]
	private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, int nFlags);

	[StructLayout(LayoutKind.Sequential)]
	private struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}
}
