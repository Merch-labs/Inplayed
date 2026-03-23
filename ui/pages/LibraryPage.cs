using System.Windows.Forms;

namespace inplayed;

public sealed class LibraryPage : UserControl
{
	public LibraryPage()
	{
		BackColor = Color.FromArgb(243, 246, 250);

		var message = new Label
		{
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleCenter,
			ForeColor = Color.FromArgb(76, 88, 102),
			Text = "Saved clips will appear in Videos\\inplayed."
		};

		Controls.Add(message);
	}
}
