using System.Windows.Forms;

internal static class UiScale
{
	public static int Px(Control control, int px)
	{
		var dpi = control.DeviceDpi > 0 ? control.DeviceDpi : 96;
		return (int)Math.Round(px * (dpi / 96.0));
	}
}