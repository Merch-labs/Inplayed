using System.Windows.Forms;

namespace inplayed;

public sealed class MainForm : Form
{
	private readonly Panel _sidebarPanel;
	private readonly FlowLayoutPanel _sidebarButtons;
	private readonly Panel _contentPanel;

	public MainForm()
	{
		AutoScaleMode = AutoScaleMode.Dpi;
		AutoScaleDimensions = new SizeF(96F, 96F);

		Text = "inplayed";
		Width = ScalePx(1100);
		Height = ScalePx(700);
		StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size(ScalePx(900), ScalePx(550));

		_sidebarPanel = new Panel
		{
			Dock = DockStyle.Left,
			Width = ScalePx(220),
			BackColor = Color.Red
		};

		_sidebarButtons = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Padding = new Padding(ScalePx(10)),
			AutoScroll = true
		};

		_contentPanel = new Panel
		{
			Dock = DockStyle.Fill
		};

		_sidebarPanel.Controls.Add(_sidebarButtons);

		Controls.Add(_contentPanel);
		Controls.Add(_sidebarPanel);
	}

	public Button AddSidebarButton(string text, EventHandler? onClick = null)
	{
		var button = new Button
		{
			Text = text,
			Width = ScalePx(190),
			Height = ScalePx(42),
			Margin = new Padding(0, 0, 0, ScalePx(8)),
			TextAlign = ContentAlignment.MiddleLeft
		};

		if (onClick != null)
		{
			button.Click += onClick;
		}

		_sidebarButtons.Controls.Add(button);
		return button;
	}

	private int ScalePx(int px)
	{
		var dpi = DeviceDpi > 0 ? DeviceDpi : 96;
		return (int)Math.Round(px * (dpi / 96.0));
	}
}
