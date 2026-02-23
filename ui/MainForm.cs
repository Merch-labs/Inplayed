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
		Width = UiScale.Px(this, 1100);
		Height = UiScale.Px(this, 700);
		StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size(UiScale.Px(this, 900), UiScale.Px(this, 550));

		_sidebarPanel = new Panel
		{
			Dock = DockStyle.Left,
			Width = UiScale.Px(this, 70),
			BackColor = Color.Red
		};

		_sidebarButtons = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			Padding = new Padding(UiScale.Px(this, 10)),
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
			Width = UiScale.Px(this, 190),
			Height = UiScale.Px(this, 42),
			Margin = new Padding(0, 0, 0, UiScale.Px(this, 8)),
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
