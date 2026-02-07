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

public sealed class DummyCapture : IVideoCapture
{
	private readonly int _width;
	private readonly int _height;

	public DummyCapture(int width, int height)
	{
		_width = width;
		_height = height;
	}

	public VideoFrame CaptureFrame()
	{
		// return empty black frame
		byte[] data = new byte[_width * _height * 4]; // RGBA
		return new VideoFrame(data, _width, _height, DateTimeOffset.Now.ToUnixTimeMilliseconds());
	}

	public void Dispose() { }
}
