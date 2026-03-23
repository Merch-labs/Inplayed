using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace inplayed;

public sealed class SocialPage : UserControl
{
	private readonly BindingSource _friendsSource = new();
	private readonly TextBox _friendNameInput;
	private readonly ComboBox _shareFileCombo;
	private readonly TextBox _captionInput;
	private readonly Label _statusLabel;
	private readonly List<string> _friends = new();

	public SocialPage()
	{
		var root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			RowCount = 2,
			ColumnCount = 1
		};
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

		var header = new Label
		{
			AutoSize = true,
			Padding = new Padding(12, 12, 12, 8),
			Text = "Social (local mock only): add friends and share clips. No backend connected yet."
		};

		var split = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Vertical,
			SplitterDistance = 300
		};

		var leftPanel = BuildFriendsPanel();
		var rightPanel = BuildSharePanel();
		split.Panel1.Controls.Add(leftPanel);
		split.Panel2.Controls.Add(rightPanel);

		root.Controls.Add(header, 0, 0);
		root.Controls.Add(split, 0, 1);
		Controls.Add(root);

		_friendNameInput = (TextBox)leftPanel.Tag!;
		_shareFileCombo = (ComboBox)rightPanel.Tag!;
		_captionInput = (TextBox)rightPanel.Controls.Find("captionInput", true)[0];
		_statusLabel = (Label)rightPanel.Controls.Find("statusLabel", true)[0];

		_friendsSource.DataSource = _friends;
		((ListBox)leftPanel.Controls.Find("friendsList", true)[0]).DataSource = _friendsSource;

		ReloadShareableFiles();
	}

	private Control BuildFriendsPanel()
	{
		var panel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			RowCount = 4,
			ColumnCount = 1,
			Padding = new Padding(12)
		};
		panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

		var title = new Label { AutoSize = true, Text = "Friends" };
		var addRow = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		var friendName = new TextBox { Width = 170, PlaceholderText = "Friend username" };
		var addButton = new Button { Text = "Add Friend", AutoSize = true };
		addButton.Click += (_, _) => AddFriend(friendName.Text);
		addRow.Controls.Add(friendName);
		addRow.Controls.Add(addButton);

		var removeButton = new Button { Text = "Remove Selected", AutoSize = true };
		removeButton.Click += (_, _) =>
		{
			var list = (ListBox)panel.Controls.Find("friendsList", true)[0];
			if (list.SelectedItem is string selected)
			{
				_friends.Remove(selected);
				RefreshFriends();
			}
		};

		var friendsList = new ListBox
		{
			Name = "friendsList",
			Dock = DockStyle.Fill
		};

		panel.Controls.Add(title, 0, 0);
		panel.Controls.Add(addRow, 0, 1);
		panel.Controls.Add(removeButton, 0, 2);
		panel.Controls.Add(friendsList, 0, 3);
		panel.Tag = friendName;
		return panel;
	}

	private Control BuildSharePanel()
	{
		var panel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			RowCount = 2,
			ColumnCount = 1,
			Padding = new Padding(12)
		};
		panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

		var shareGroup = new GroupBox
		{
			Text = "Share a Video",
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

		var shareFileCombo = new ComboBox
		{
			Dock = DockStyle.Fill,
			DropDownStyle = ComboBoxStyle.DropDownList
		};
		var captionInput = new TextBox
		{
			Name = "captionInput",
			Dock = DockStyle.Fill,
			PlaceholderText = "Enter a caption"
		};
		var refreshFilesButton = new Button { Text = "Refresh Clips", AutoSize = true };
		var shareButton = new Button { Text = "Share", AutoSize = true };
		refreshFilesButton.Click += (_, _) => ReloadShareableFiles();
		shareButton.Click += (_, _) => ShareClip();

		var buttonRow = new FlowLayoutPanel
		{
			AutoSize = true,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			Dock = DockStyle.Fill
		};
		buttonRow.Controls.Add(refreshFilesButton);
		buttonRow.Controls.Add(shareButton);

		shareLayout.Controls.Add(new Label { AutoSize = true, Text = "Video", Margin = new Padding(0, 7, 8, 0) }, 0, 0);
		shareLayout.Controls.Add(shareFileCombo, 1, 0);
		shareLayout.Controls.Add(new Label { AutoSize = true, Text = "Caption", Margin = new Padding(0, 7, 8, 0) }, 0, 1);
		shareLayout.Controls.Add(captionInput, 1, 1);
		shareLayout.Controls.Add(buttonRow, 1, 2);
		shareGroup.Controls.Add(shareLayout);

		var statusRow = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};
		var statusLabel = new Label
		{
			Name = "statusLabel",
			AutoSize = true,
			Padding = new Padding(0, 7, 0, 0),
			Text = "Status: local mock social"
		};
		statusRow.Controls.Add(statusLabel);

		panel.Controls.Add(shareGroup, 0, 0);
		panel.Controls.Add(statusRow, 0, 1);
		panel.Tag = shareFileCombo;
		return panel;
	}

	private void AddFriend(string rawName)
	{
		var name = rawName.Trim();
		if (string.IsNullOrWhiteSpace(name))
		{
			_statusLabel.Text = "Status: enter a friend name";
			return;
		}

		if (_friends.Any(f => string.Equals(f, name, StringComparison.OrdinalIgnoreCase)))
		{
			_statusLabel.Text = "Status: friend already exists";
			return;
		}

		_friends.Add(name);
		_friendNameInput.Clear();
		RefreshFriends();
		_statusLabel.Text = $"Status: added friend '{name}' (local only)";
	}

	private void RefreshFriends()
	{
		_friendsSource.DataSource = null;
		_friendsSource.DataSource = _friends.OrderBy(name => name).ToList();
	}

	private void ReloadShareableFiles()
	{
		var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "inplayed");
		Directory.CreateDirectory(folder);

		var files = Directory
			.EnumerateFiles(folder, "*.mp4")
			.OrderByDescending(File.GetLastWriteTimeUtc)
			.ToList();

		_shareFileCombo.DataSource = files;
		if (_shareFileCombo.Items.Count > 0)
		{
			_shareFileCombo.SelectedIndex = 0;
			_statusLabel.Text = $"Status: loaded {_shareFileCombo.Items.Count} local clip(s)";
		}
		else
		{
			_statusLabel.Text = "Status: no clips found in Videos\\inplayed";
		}
	}

	private void ShareClip()
	{
		if (_shareFileCombo.SelectedItem is not string path || string.IsNullOrWhiteSpace(path))
		{
			_statusLabel.Text = "Status: select a clip to share";
			return;
		}

		var caption = string.IsNullOrWhiteSpace(_captionInput.Text) ? "Shared a clip" : _captionInput.Text.Trim();
		_captionInput.Clear();
		_statusLabel.Text = $"Status: queued '{Path.GetFileName(path)}' ({caption}) for sharing when backend is ready";
	}
}
