using System.IO;
using System.Windows.Forms;

namespace inplayed;

public sealed class MainForm : Form
{
	private readonly Panel _sidebarPanel;
	private readonly FlowLayoutPanel _sidebarButtons;
	private readonly Panel _contentPanel;
	private readonly Dictionary<string, UserControl> _pages = new(StringComparer.OrdinalIgnoreCase);

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
			Padding = new Padding(UiScale.Px(this, 0)),
			AutoScroll = true
		};

		_contentPanel = new Panel
		{
			Dock = DockStyle.Fill
		};

		_sidebarPanel.Controls.Add(_sidebarButtons);

		Controls.Add(_contentPanel);
		Controls.Add(_sidebarPanel);

		AddSidebarButton("home", (_, _) => ShowPage("home"));
		ShowPage("home");
	}

	public Button AddSidebarButton(string name, EventHandler? onClick = null)
	{
		var button = new Button
		{
			BackgroundImage = LoadIcon(name),
			BackgroundImageLayout = ImageLayout.Zoom,
			FlatStyle = FlatStyle.Flat,
			Width = UiScale.Px(this, 70),
			Height = UiScale.Px(this, 70),
			Margin = new Padding(0, 0, 0, 0),
			TextAlign = ContentAlignment.MiddleLeft,
		};
		button.FlatAppearance.BorderSize = 0;


		if (onClick != null)
		{
			button.Click += onClick;
		}

		_sidebarButtons.Controls.Add(button);
		return button;
	}

	private void ShowPage(string key)
	{
		if (!_pages.TryGetValue(key, out var page))
		{
			page = key switch
			{
				"home" => new HomePage()
			};

			page.Dock = DockStyle.Fill;
			_pages[key] = page;
		}

		_contentPanel.Controls.Clear();
		_contentPanel.Controls.Add(page);
	}

	private Image LoadIcon(string name)
	{
		string basePath = Path.Combine(AppContext.BaseDirectory, "ui", "icons");
		string requestedPath = Path.Combine(basePath, "/{name}.png");
		string defaultPath = Path.Combine(basePath, "place-holder.png");

		string path = File.Exists(requestedPath) ? requestedPath : defaultPath;

		using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
		{
			return Image.FromStream(fs);
		}
	}
}
