using System.IO;
using System.Windows.Forms;

namespace inplayed;

public sealed class MainForm : Form
{
	private readonly CaptureController _captureController = new();
	private readonly AppLaunchOptions _launchOptions;
	private GlobalHotkey? _saveClipHotkey;
	private readonly NotifyIcon _trayIcon;
	private readonly ContextMenuStrip _trayMenu;
	private readonly Panel _topBarPanel;
	private readonly Label _topBarTitle;
	private readonly Panel _bodyPanel;
	private readonly Panel _sidebarPanel;
	private readonly FlowLayoutPanel _sidebarButtons;
	private readonly Panel _contentPanel;
	private readonly Dictionary<string, UserControl> _pages = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Button> _sidebarButtonsByPage = new(StringComparer.OrdinalIgnoreCase);
	private bool _allowExit;
	private bool _hiddenToTray;

	internal MainForm(AppLaunchOptions? launchOptions = null)
	{
		_launchOptions = launchOptions ?? new AppLaunchOptions();
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

		_trayMenu = new ContextMenuStrip();
		_trayMenu.Items.Add("Open inplayed", null, (_, _) => ShowFromTray());
		_trayMenu.Items.Add("Start Capture", null, async (_, _) => await StartCaptureFromTrayAsync());
		_trayMenu.Items.Add("Stop Capture", null, async (_, _) => await StopCaptureFromTrayAsync());
		_trayMenu.Items.Add("Save Clip", null, async (_, _) => await SaveClipFromTrayAsync());
		_trayMenu.Items.Add(new ToolStripSeparator());
		_trayMenu.Items.Add("Exit", null, (_, _) => ExitApplication());

		_trayIcon = new NotifyIcon
		{
			Text = "inplayed",
			Icon = SystemIcons.Application,
			ContextMenuStrip = _trayMenu,
			Visible = true
		};
		_trayIcon.DoubleClick += (_, _) => ShowFromTray();

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
		ApplySettings();
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
				"settings" => new SettingsPage(ApplySettings),
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

	public void StartHiddenMode()
	{
		ShowInTaskbar = false;
		if (!IsHandleCreated)
		{
			_ = Handle;
		}

		HideToTray(showNotification: false);
		if (_launchOptions.AutoStartCapture)
		{
			BeginInvoke(async () => await StartCaptureFromTrayAsync());
		}
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

	private void ApplySettings()
	{
		ReloadSaveClipHotkey();
		UpdateTrayTooltip();
	}

	private void UpdateTrayTooltip()
	{
		var sessionStatus = _captureController.GetSessionStatus();
		_trayIcon.Text = sessionStatus.Length > 50
			? $"inplayed - {sessionStatus[..47]}..."
			: $"inplayed - {sessionStatus}";
	}

	private void ShowFromTray()
	{
		_hiddenToTray = false;
		ShowInTaskbar = true;
		Show();
		WindowState = FormWindowState.Normal;
		Activate();
	}

	private void HideToTray(bool showNotification)
	{
		_hiddenToTray = true;
		ShowInTaskbar = false;
		Hide();

		if (showNotification)
		{
			_trayIcon.BalloonTipTitle = "inplayed";
			_trayIcon.BalloonTipText = "Still running in the background for clipping.";
			_trayIcon.ShowBalloonTip(2000);
		}
	}

	private async Task StartCaptureFromTrayAsync()
	{
		try
		{
			await _captureController.StartCapture();
		}
		catch (Exception ex)
		{
			ShowTrayError("Start capture failed", ex.Message);
		}

		UpdateTrayTooltip();
	}

	private async Task StopCaptureFromTrayAsync()
	{
		try
		{
			await _captureController.StopCapture();
		}
		catch (Exception ex)
		{
			ShowTrayError("Stop capture failed", ex.Message);
		}

		UpdateTrayTooltip();
	}

	private async Task SaveClipFromTrayAsync()
	{
		try
		{
			await _captureController.SaveClip();
		}
		catch (Exception ex)
		{
			ShowTrayError("Save clip failed", ex.Message);
		}

		UpdateTrayTooltip();
	}

	private void ShowTrayError(string title, string message)
	{
		if (Visible)
		{
			MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
			return;
		}

		_trayIcon.BalloonTipTitle = title;
		_trayIcon.BalloonTipText = message;
		_trayIcon.ShowBalloonTip(2500);
	}

	private bool ShouldCloseToTray()
	{
		var config = AppConfig.Load();
		return _launchOptions.StartHidden || config.Startup.LaunchOnWindowsStartup;
	}

	private void ExitApplication()
	{
		_allowExit = true;
		Close();
	}

	protected override void OnFormClosing(FormClosingEventArgs e)
	{
		if (!_allowExit && e.CloseReason == CloseReason.UserClosing && ShouldCloseToTray())
		{
			e.Cancel = true;
			HideToTray(showNotification: !_hiddenToTray);
			return;
		}

		base.OnFormClosing(e);
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_trayIcon.Visible = false;
			_trayIcon.Dispose();
			_trayMenu.Dispose();
			_saveClipHotkey?.Dispose();
			_captureController.Dispose();
		}

		base.Dispose(disposing);
	}
}
