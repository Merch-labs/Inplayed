using System.IO;
using System.Windows.Forms;

namespace inplayed;

public sealed class MainForm : Form
{
	private readonly CaptureController _captureController = new();
	private GlobalHotkey? _saveClipHotkey;
	private readonly Panel _topBarPanel;
	private readonly Label _topBarTitle;
	private readonly Panel _bodyPanel;
	private readonly Panel _sidebarPanel;
	private readonly FlowLayoutPanel _sidebarButtons;
	private readonly Panel _contentPanel;
	private readonly Dictionary<string, UserControl> _pages = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Button> _sidebarButtonsByPage = new(StringComparer.OrdinalIgnoreCase);

	public MainForm()
	{
		AutoScaleMode = AutoScaleMode.Dpi;
		AutoScaleDimensions = new SizeF(96F, 96F);

		Text = "inplayed";
		Width = UiScale.Px(this, 1100);
		Height = UiScale.Px(this, 700);
		StartPosition = FormStartPosition.CenterScreen;
		MinimumSize = new Size(UiScale.Px(this, 900), UiScale.Px(this, 550));
		BackColor = UiTheme.ShellBackground;

		_topBarPanel = new Panel
		{
			Dock = DockStyle.Top,
			Height = UiScale.Px(this, 48),
			BackColor = UiTheme.SurfaceBackground
		};
		_topBarTitle = new Label
		{
			Dock = DockStyle.Fill,
			TextAlign = ContentAlignment.MiddleLeft,
			Padding = new Padding(UiScale.Px(this, 12), 0, 0, 0),
			ForeColor = UiTheme.TitleColor,
			Font = new Font("Segoe UI Semibold", 12f, FontStyle.Regular, GraphicsUnit.Point)
		};
		_topBarPanel.Controls.Add(_topBarTitle);

		_bodyPanel = new Panel
		{
			Dock = DockStyle.Fill,
			BackColor = UiTheme.ShellBackground
		};

		_sidebarPanel = new Panel
		{
			Dock = DockStyle.Left,
			Width = UiScale.Px(this, 70),
			BackColor = UiTheme.SidebarBackground
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
			Dock = DockStyle.Fill,
			BackColor = UiTheme.ShellBackground,
			Padding = new Padding(UiScale.Px(this, 12))
		};

		_sidebarPanel.Controls.Add(_sidebarButtons);

		_bodyPanel.Controls.Add(_contentPanel);
		_bodyPanel.Controls.Add(_topBarPanel);

		Controls.Add(_bodyPanel);
		Controls.Add(_sidebarPanel);

		AddSidebarButton("recording", (_, _) => ShowPage("recording"));
		AddSidebarButton("library", (_, _) => ShowPage("library"));
		AddSidebarButton("social", (_, _) => ShowPage("social"));
		AddSidebarButton("settings", (_, _) => ShowPage("settings"));
		ShowPage("recording");
		ReloadSaveClipHotkey();
	}

	public Button AddSidebarButton(string name, EventHandler? onClick = null)
	{
		var button = new Button
		{
			BackgroundImage = LoadIcon(name),
			BackgroundImageLayout = ImageLayout.Zoom,
			BackColor = UiTheme.SidebarBackground,
			ForeColor = Color.White,
			FlatStyle = FlatStyle.Flat,
			Width = UiScale.Px(this, 70),
			Height = UiScale.Px(this, 70),
			Margin = new Padding(0, 0, 0, 0),
			TextAlign = ContentAlignment.MiddleLeft,
			UseVisualStyleBackColor = false
		};
		button.FlatAppearance.BorderSize = 0;
		button.FlatAppearance.MouseOverBackColor = UiTheme.SidebarHover;
		button.FlatAppearance.MouseDownBackColor = UiTheme.SidebarActive;


		if (onClick != null)
		{
			button.Click += onClick;
		}

		_sidebarButtons.Controls.Add(button);
		_sidebarButtonsByPage[name] = button;
		return button;
	}

	private void ShowPage(string key)
	{
		if (!_pages.TryGetValue(key, out var page))
		{
			page = key switch
			{
				"recording" => new RecordingPage(_captureController),
				"library" => new LibraryPage(),
				"social" => new SocialPage(),
				"settings" => new SettingsPage(ReloadSaveClipHotkey),
				_ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown page key.")
			};

			page.Dock = DockStyle.Fill;
			_pages[key] = page;
		}

		_contentPanel.Controls.Clear();
		_contentPanel.Controls.Add(page);
		_topBarTitle.Text = char.ToUpperInvariant(key[0]) + key[1..];
		UpdateSidebarSelection(key);
	}

	private Image LoadIcon(string name)
	{
		string basePath = Path.Combine(AppContext.BaseDirectory, "ui", "icons");
		string requestedPath = Path.Combine(basePath, $"{name}.png");
		string defaultPath = Path.Combine(basePath, "place-holder.png");

		string path = File.Exists(requestedPath) ? requestedPath : defaultPath;

		using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
		{
			return Image.FromStream(fs);
		}
	}

	private void UpdateSidebarSelection(string selectedPage)
	{
		foreach (var entry in _sidebarButtonsByPage)
		{
			var isActive = string.Equals(entry.Key, selectedPage, StringComparison.OrdinalIgnoreCase);
			entry.Value.BackColor = isActive ? UiTheme.SidebarActive : UiTheme.SidebarBackground;
		}
	}

	private void ReloadSaveClipHotkey()
	{
		_saveClipHotkey?.Dispose();
		_saveClipHotkey = null;

		var config = AppConfig.Load();
		var (modifiers, key) = config.GetSaveClipHotkey();
		_saveClipHotkey = new GlobalHotkey(this, modifiers, key);
		_saveClipHotkey.Pressed += async (_, _) =>
		{
			try
			{
				await _captureController.SaveClip();
			}
			catch (Exception ex)
			{
				if (IsHandleCreated)
				{
					BeginInvoke(() => MessageBox.Show(this, ex.Message, "Save clip failed", MessageBoxButtons.OK, MessageBoxIcon.Error));
				}
			}
		};
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_saveClipHotkey?.Dispose();
			_captureController.Dispose();
		}

		base.Dispose(disposing);
	}
}
