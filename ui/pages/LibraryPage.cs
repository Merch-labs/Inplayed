using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace inplayed;

public sealed class LibraryPage : UserControl
{
	private readonly SocialService _socialService = new();
	private readonly ListView _clipsListView;
	private readonly ComboBox _friendComboBox;
	private readonly TextBox _captionInput;
	private readonly Button _shareButton;
	private readonly Label _statusLabel;

	public LibraryPage()
	{
		BackColor = UiTheme.ShellBackground;

		var root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			RowCount = 4,
			ColumnCount = 1,
			Padding = new Padding(16),
			BackColor = UiTheme.ShellBackground
		};
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

		var header = new Label
		{
			AutoSize = true,
			Padding = new Padding(0, 0, 0, 8),
			ForeColor = UiTheme.SecondaryTextColor,
			Text = "Saved clips from Videos\\inplayed"
		};

		_clipsListView = new ListView
		{
			Dock = DockStyle.Fill,
			View = View.Details,
			FullRowSelect = true,
			GridLines = false,
			MultiSelect = false,
			HideSelection = false,
			BackColor = UiTheme.SurfaceBackground
		};
		_clipsListView.Columns.Add("Clip", 340);
		_clipsListView.Columns.Add("Modified", 180);
		_clipsListView.Columns.Add("Size", 120);
		_clipsListView.DoubleClick += (_, _) => OpenSelectedClip();
		_clipsListView.SelectedIndexChanged += (_, _) => UpdateInteractionState();

		var actions = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			AutoSize = true,
			Padding = new Padding(0, 8, 0, 0),
			BackColor = UiTheme.ShellBackground
		};

		var refreshButton = new Button { Text = "Refresh", AutoSize = true };
		var openFolderButton = new Button { Text = "Open Folder", AutoSize = true };
		var openClipButton = new Button { Text = "Open Selected", AutoSize = true };
		UiTheme.StyleAccentButton(refreshButton);
		UiTheme.StyleSecondaryButton(openFolderButton);
		UiTheme.StyleSecondaryButton(openClipButton);
		refreshButton.Click += (_, _) => ReloadData();
		openFolderButton.Click += (_, _) => OpenLibraryFolder();
		openClipButton.Click += (_, _) => OpenSelectedClip();

		actions.Controls.Add(refreshButton);
		actions.Controls.Add(openFolderButton);
		actions.Controls.Add(openClipButton);

		var shareGroup = new GroupBox
		{
			Text = "Share Selected Clip",
			Dock = DockStyle.Top,
			AutoSize = true
		};
		var shareLayout = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			ColumnCount = 2,
			RowCount = 3,
			Padding = new Padding(8)
		};
		shareLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		shareLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

		_friendComboBox = new ComboBox
		{
			Dock = DockStyle.Fill,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		_friendComboBox.SelectedIndexChanged += (_, _) => UpdateInteractionState();
		_captionInput = new TextBox
		{
			Dock = DockStyle.Fill,
			PlaceholderText = "Add a caption"
		};
		_shareButton = new Button
		{
			Text = "Share to Chat",
			AutoSize = true
		};
		UiTheme.StyleAccentButton(_shareButton);
		_shareButton.Click += (_, _) => ShareSelectedClip();

		shareLayout.Controls.Add(new Label { AutoSize = true, Text = "Friend", Margin = new Padding(0, 7, 8, 0) }, 0, 0);
		shareLayout.Controls.Add(_friendComboBox, 1, 0);
		shareLayout.Controls.Add(new Label { AutoSize = true, Text = "Caption", Margin = new Padding(0, 7, 8, 0) }, 0, 1);
		shareLayout.Controls.Add(_captionInput, 1, 1);
		shareLayout.Controls.Add(_shareButton, 1, 2);
		shareGroup.Controls.Add(shareLayout);

		_statusLabel = new Label
		{
			AutoSize = true,
			Padding = new Padding(0, 12, 0, 0),
			ForeColor = UiTheme.SecondaryTextColor,
			Text = "Status: ready"
		};

		root.Controls.Add(header, 0, 0);
		root.Controls.Add(_clipsListView, 0, 1);
		root.Controls.Add(actions, 0, 2);
		root.Controls.Add(shareGroup, 0, 3);
		root.Controls.Add(_statusLabel, 0, 4);
		root.RowCount = 5;
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

		Controls.Add(root);
		ReloadData();
	}

	protected override void OnVisibleChanged(EventArgs e)
	{
		base.OnVisibleChanged(e);
		if (Visible)
		{
			ReloadData();
		}
	}

	private void ReloadData()
	{
		ReloadClips();
		ReloadFriends();
		UpdateInteractionState();
	}

	private void ReloadClips()
	{
		var folder = GetLibraryFolder();
		Directory.CreateDirectory(folder);

		var files = Directory
			.EnumerateFiles(folder, "*.mp4")
			.Select(path => new FileInfo(path))
			.OrderByDescending(file => file.LastWriteTimeUtc)
			.ToList();

		_clipsListView.BeginUpdate();
		try
		{
			_clipsListView.Items.Clear();
			foreach (var file in files)
			{
				var item = new ListViewItem(file.Name)
				{
					Tag = file.FullName
				};
				item.SubItems.Add(file.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
				item.SubItems.Add(FormatSize(file.Length));
				_clipsListView.Items.Add(item);
			}
		}
		finally
		{
			_clipsListView.EndUpdate();
		}

		_statusLabel.Text = files.Count > 0
			? $"Status: loaded {files.Count} clip(s)"
			: "Status: no clips found in Videos\\inplayed";
	}

	private void ReloadFriends()
	{
		if (!_socialService.IsLoggedIn)
		{
			_friendComboBox.DataSource = new List<string>();
			return;
		}

		var friends = _socialService.GetFriends().ToList();
		_friendComboBox.DataSource = friends;
		if (friends.Count > 0)
		{
			_friendComboBox.SelectedIndex = 0;
		}
	}

	private void OpenLibraryFolder()
	{
		var folder = GetLibraryFolder();
		Directory.CreateDirectory(folder);
		Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
	}

	private void OpenSelectedClip()
	{
		if (GetSelectedClipPath() is not string path || !File.Exists(path))
		{
			_statusLabel.Text = "Status: select a clip to open";
			return;
		}

		Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
		_statusLabel.Text = $"Status: opened {Path.GetFileName(path)}";
	}

	private void ShareSelectedClip()
	{
		if (!_socialService.IsLoggedIn)
		{
			_statusLabel.Text = "Status: log in on the Social page first";
			return;
		}

		var path = GetSelectedClipPath();
		if (string.IsNullOrWhiteSpace(path))
		{
			_statusLabel.Text = "Status: select a clip to share";
			return;
		}

		if (_friendComboBox.SelectedItem is not string friendName || string.IsNullOrWhiteSpace(friendName))
		{
			_statusLabel.Text = "Status: add a friend in Social first";
			return;
		}

		var message = _socialService.ShareClip(friendName, path, _captionInput.Text);
		if (message == null)
		{
			_statusLabel.Text = "Status: failed to share clip";
			return;
		}

		_captionInput.Clear();
		_statusLabel.Text = $"Status: shared '{Path.GetFileName(path)}' with {friendName}";
	}

	private string? GetSelectedClipPath()
	{
		if (_clipsListView.SelectedItems.Count == 0)
		{
			return null;
		}

		return _clipsListView.SelectedItems[0].Tag as string;
	}

	private void UpdateInteractionState()
	{
		var hasClip = !string.IsNullOrWhiteSpace(GetSelectedClipPath());
		var hasFriend = _friendComboBox.Items.Count > 0 && _friendComboBox.SelectedItem is string friend && !string.IsNullOrWhiteSpace(friend);
		_shareButton.Enabled = hasClip && hasFriend;
		_captionInput.Enabled = hasClip && hasFriend;
		_friendComboBox.Enabled = _friendComboBox.Items.Count > 0;
	}

	private static string GetLibraryFolder()
	{
		return Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
			"inplayed");
	}

	private static string FormatSize(long bytes)
	{
		const double kilobyte = 1024d;
		const double megabyte = kilobyte * 1024d;
		const double gigabyte = megabyte * 1024d;

		if (bytes >= gigabyte)
		{
			return $"{bytes / gigabyte:0.0} GB";
		}

		if (bytes >= megabyte)
		{
			return $"{bytes / megabyte:0.0} MB";
		}

		if (bytes >= kilobyte)
		{
			return $"{bytes / kilobyte:0.0} KB";
		}

		return $"{bytes} B";
	}
}
