using System.Windows.Forms;

namespace inplayed;

public sealed class MainForm : Form
{
	public MainForm()
	{
		Text = "inplayed";
		Width = 900;
		Height = 600;
		StartPosition = FormStartPosition.CenterScreen;
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);
	}
}
