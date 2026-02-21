using System.Windows.Forms;

namespace inplayed;

public sealed class MainForm : Form
{
	private readonly Panel _sidebarPanel;
	private readonly FlowLayoutPanel _sidebarButtons;
	private readonly Panel _contentPanel;

	public MainForm()
	{
		Text = "inplayed";
		Width = 1100;
		Height = 700;
		StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size(900, 550);

		_sidebarPanel = new Panel
		{
			Dock = DockStyle.Left,
			Width = 220
		};

		_sidebarButtons = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Padding = new Padding(10),
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
			Width = 190,
			Height = 42,
			Margin = new Padding(0, 0, 0, 8),
			TextAlign = ContentAlignment.MiddleLeft
		};

		if (onClick != null)
		{
			button.Click += onClick;
		}

		_sidebarButtons.Controls.Add(button);
		return button;
	}
}
