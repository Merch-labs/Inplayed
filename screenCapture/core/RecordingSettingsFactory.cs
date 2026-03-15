using System.Drawing;
using System.Windows.Forms;

namespace inplayed;

internal static class RecordingSettingsFactory
{
	public static RecordingSettings Create(AppConfig config)
	{
		var screens = Screen.AllScreens.Select(screen => screen.Bounds).ToArray();
		var primaryMonitorIndex = ActiveTargetResolver.GetPrimaryMonitorIndex();

		return Create(
			config,
			screens,
			primaryMonitorIndex,
			ActiveTargetResolver.GetActiveMonitorIndex,
			ActiveTargetResolver.GetActiveWindowHandle,
			ActiveTargetResolver.GetWindowHandleByExecutablePath,
			ActiveTargetResolver.GetWindowBounds);
	}

	internal static RecordingSettings Create(
		AppConfig config,
		IReadOnlyList<Rectangle> screenBounds,
		int primaryMonitorIndex,
		Func<int> getActiveMonitorIndex,
		Func<IntPtr> getActiveWindowHandle,
		Func<string, IntPtr> getWindowHandleByExecutablePath,
		Func<IntPtr, Rectangle> getWindowBounds)
	{
		var captureTarget = config.Recording.CaptureTarget;
		var (target, bounds) = ResolveTarget(
			captureTarget,
			screenBounds,
			primaryMonitorIndex,
			getActiveMonitorIndex,
			getActiveWindowHandle,
			getWindowHandleByExecutablePath,
			getWindowBounds);

		return new RecordingSettings
		{
			Width = Math.Max(1, bounds.Width),
			Height = Math.Max(1, bounds.Height),
			Fps = config.Recording.Fps,
			Bitrate = config.Recording.BitrateMbps * 1_000_000,
			ClipSeconds = config.Recording.ClipSeconds,
			UseNativeNvenc = config.NativeNvencEnabled,
			Target = target
		};
	}

	private static (CaptureTarget Target, Rectangle Bounds) ResolveTarget(
		AppConfig.CaptureTargetConfig captureTarget,
		IReadOnlyList<Rectangle> screenBounds,
		int primaryMonitorIndex,
		Func<int> getActiveMonitorIndex,
		Func<IntPtr> getActiveWindowHandle,
		Func<string, IntPtr> getWindowHandleByExecutablePath,
		Func<IntPtr, Rectangle> getWindowBounds)
	{
		var mode = captureTarget.Mode;
		if (string.Equals(mode, CaptureTargetModes.ActiveWindow, StringComparison.OrdinalIgnoreCase))
		{
			return ResolveWindowTarget(getActiveWindowHandle(), getWindowBounds, "No active window is available to capture.");
		}

		if (string.Equals(mode, CaptureTargetModes.ExecutablePath, StringComparison.OrdinalIgnoreCase))
		{
			if (string.IsNullOrWhiteSpace(captureTarget.ExecutablePath))
			{
				throw new InvalidOperationException("An executable path is required for executable capture mode.");
			}

			var hwnd = getWindowHandleByExecutablePath(captureTarget.ExecutablePath);
			return ResolveWindowTarget(hwnd, getWindowBounds, "No running window matched the configured executable path.");
		}

		if (string.Equals(mode, CaptureTargetModes.SpecificMonitor, StringComparison.OrdinalIgnoreCase))
		{
			return ResolveMonitorTarget(screenBounds, captureTarget.MonitorIndex);
		}

		if (string.Equals(mode, CaptureTargetModes.PrimaryMonitor, StringComparison.OrdinalIgnoreCase))
		{
			return ResolveMonitorTarget(screenBounds, primaryMonitorIndex);
		}

		return ResolveMonitorTarget(screenBounds, getActiveMonitorIndex());
	}

	private static (CaptureTarget Target, Rectangle Bounds) ResolveMonitorTarget(IReadOnlyList<Rectangle> screenBounds, int monitorIndex)
	{
		var index = NormalizeMonitorIndex(screenBounds, monitorIndex);
		var bounds = GetMonitorBounds(screenBounds, index);
		return (new MonitorTarget { MonitorIndex = index }, bounds);
	}

	private static (CaptureTarget Target, Rectangle Bounds) ResolveWindowTarget(
		IntPtr hwnd,
		Func<IntPtr, Rectangle> getWindowBounds,
		string missingWindowMessage)
	{
		if (hwnd == IntPtr.Zero)
		{
			throw new InvalidOperationException(missingWindowMessage);
		}

		var bounds = getWindowBounds(hwnd);
		if (bounds.Width <= 0 || bounds.Height <= 0)
		{
			throw new InvalidOperationException("The selected capture window is not visible.");
		}

		return (new WindowTarget { Hwnd = hwnd }, bounds);
	}

	private static int NormalizeMonitorIndex(IReadOnlyList<Rectangle> screenBounds, int monitorIndex)
	{
		if (screenBounds.Count == 0)
		{
			return 0;
		}

		return monitorIndex >= 0 && monitorIndex < screenBounds.Count ? monitorIndex : 0;
	}

	private static Rectangle GetMonitorBounds(IReadOnlyList<Rectangle> screenBounds, int monitorIndex)
	{
		if (screenBounds.Count == 0)
		{
			return new Rectangle(0, 0, 1920, 1080);
		}

		return screenBounds[NormalizeMonitorIndex(screenBounds, monitorIndex)];
	}
}
