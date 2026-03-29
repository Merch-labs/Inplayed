using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace inplayed;

public sealed class SocialPage : UserControl
{
	private static readonly Color ClipBubbleColor = Color.FromArgb(232, 237, 243);
	private readonly BindingSource _friendsSource = new();
	private readonly SocialService _socialService = new();
	private readonly SplitContainer _splitContainer;
	private readonly ListBox _friendsList;
	private readonly TextBox _friendNameInput;
	private readonly Button _backButton;
	private readonly Label _conversationTitleLabel;
	private readonly FlowLayoutPanel _messagesPanel;
	private readonly TextBox _messageInput;
	private readonly Button _sendButton;
	private readonly Label _statusLabel;

	public SocialPage()
	{
		BackColor = UiTheme.ShellBackground;

		var root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			RowCount = 2,
			ColumnCount = 1,
			Padding = new Padding(12),
			BackColor = UiTheme.ShellBackground
		};
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

		var header = new Label
		{
			AutoSize = true,
			Padding = new Padding(0, 0, 0, 8),
			Text = "Messages and shared clips from your conversations.",
			ForeColor = UiTheme.SecondaryTextColor
		};

		_splitContainer = new SplitContainer
		{
			Dock = DockStyle.Fill,
			Orientation = Orientation.Vertical,
			SplitterDistance = 240,
			BackColor = UiTheme.ShellBackground
		};

		var leftPanel = BuildFriendsPanel();
		var rightPanel = BuildConversationPanel();
		_splitContainer.Panel1.Controls.Add(leftPanel);
		_splitContainer.Panel2.Controls.Add(rightPanel);

		root.Controls.Add(header, 0, 0);
		root.Controls.Add(_splitContainer, 0, 1);
		Controls.Add(root);

		_friendNameInput = (TextBox)leftPanel.Controls.Find("friendNameInput", true)[0];
		_friendsList = (ListBox)leftPanel.Controls.Find("friendsList", true)[0];
		_backButton = (Button)rightPanel.Controls.Find("backButton", true)[0];
		_conversationTitleLabel = (Label)rightPanel.Controls.Find("conversationTitleLabel", true)[0];
		_messagesPanel = (FlowLayoutPanel)rightPanel.Controls.Find("messagesPanel", true)[0];
		_messageInput = (TextBox)rightPanel.Controls.Find("messageInput", true)[0];
		_sendButton = (Button)rightPanel.Controls.Find("sendButton", true)[0];
		_statusLabel = (Label)rightPanel.Controls.Find("statusLabel", true)[0];

		var friends = new List<string>();
		foreach (var friend in _socialService.GetFriends())
		{
			friends.Add(friend);
		}

		_friendsSource.DataSource = friends;
		_friendsList.DataSource = _friendsSource;
		_friendsList.SelectedIndexChanged += (_, _) => RefreshConversation();
		_friendsList.DoubleClick += (_, _) => OpenSelectedFriendConversation();
		_messagesPanel.Resize += (_, _) => ResizeMessageCards();
		_messageInput.KeyDown += HandleMessageInputKeyDown;

		UiTheme.ApplyPalette(this);
		RefreshFriends();
		ShowProfilesOnly();
	}

	protected override void OnVisibleChanged(EventArgs e)
	{
		base.OnVisibleChanged(e);
		if (Visible)
		{
			RefreshFriends(GetSelectedFriend());
		}
	}

	private Control BuildFriendsPanel()
	{
		var panel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			RowCount = 4,
			ColumnCount = 1,
			Padding = new Padding(12),
			BackColor = UiTheme.ShellBackground
		};
		panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

		var title = new Label
		{
			AutoSize = true,
			Text = "Friends",
			ForeColor = UiTheme.SecondaryTextColor
		};

		var addRow = new TableLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			ColumnCount = 2,
			BackColor = UiTheme.ShellBackground
		};
		addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		addRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

		var friendName = new TextBox
		{
			Name = "friendNameInput",
			Dock = DockStyle.Fill,
			PlaceholderText = "Friend username"
		};
		var addButton = new Button
		{
			Text = "Add",
			AutoSize = true
		};
		UiTheme.StyleAccentButton(addButton);
		addButton.Click += (_, _) => AddFriend(friendName.Text);
		addRow.Controls.Add(friendName, 0, 0);
		addRow.Controls.Add(addButton, 1, 0);

		var removeButton = new Button
		{
			Text = "Remove Selected",
			AutoSize = true
		};
		UiTheme.StyleSecondaryButton(removeButton);
		removeButton.Click += (_, _) => RemoveSelectedFriend();

		var friendsList = new ListBox
		{
			Name = "friendsList",
			Dock = DockStyle.Fill,
			BackColor = UiTheme.SurfaceBackground
		};

		panel.Controls.Add(title, 0, 0);
		panel.Controls.Add(addRow, 0, 1);
		panel.Controls.Add(removeButton, 0, 2);
		panel.Controls.Add(friendsList, 0, 3);
		return panel;
	}

	private Control BuildConversationPanel()
	{
		var panel = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			RowCount = 4,
			ColumnCount = 1,
			Padding = new Padding(12),
			BackColor = UiTheme.ShellBackground
		};
		panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

		var headerRow = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			WrapContents = false,
			FlowDirection = FlowDirection.LeftToRight,
			BackColor = UiTheme.ShellBackground
		};

		var backButton = new Button
		{
			Name = "backButton",
			Text = "Back",
			AutoSize = true,
			Visible = false
		};
		UiTheme.StyleSecondaryButton(backButton);
		backButton.Click += (_, _) => ReturnToProfiles();

		var title = new Label
		{
			Name = "conversationTitleLabel",
			AutoSize = true,
			Padding = new Padding(0, 7, 0, 0),
			Text = "Select a friend to start messaging",
			ForeColor = UiTheme.SecondaryTextColor
		};
		headerRow.Controls.Add(backButton);
		headerRow.Controls.Add(title);

		var messagesPanel = new FlowLayoutPanel
		{
			Name = "messagesPanel",
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false,
			AutoScroll = true,
			BackColor = UiTheme.SurfaceBackground,
			Padding = new Padding(12)
		};

		var messageGroup = new GroupBox
		{
			Text = "Send Message",
			Dock = DockStyle.Top,
			AutoSize = true
		};

		var messageLayout = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			ColumnCount = 2,
			Padding = new Padding(8)
		};
		messageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
		messageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

		var messageInput = new TextBox
		{
			Name = "messageInput",
			Dock = DockStyle.Fill,
			PlaceholderText = "Write a message"
		};

		var sendButton = new Button
		{
			Name = "sendButton",
			Text = "Send",
			AutoSize = true
		};
		UiTheme.StyleAccentButton(sendButton);
		sendButton.Click += (_, _) => SendMessage();

		messageLayout.Controls.Add(messageInput, 0, 0);
		messageLayout.Controls.Add(sendButton, 1, 0);
		messageGroup.Controls.Add(messageLayout);

		var statusLabel = new Label
		{
			Name = "statusLabel",
			AutoSize = true,
			Padding = new Padding(0, 8, 0, 0),
			ForeColor = UiTheme.SecondaryTextColor,
			Text = "Status: ready"
		};

		panel.Controls.Add(headerRow, 0, 0);
		panel.Controls.Add(messagesPanel, 0, 1);
		panel.Controls.Add(messageGroup, 0, 2);
		panel.Controls.Add(statusLabel, 0, 3);
		return panel;
	}

	private void AddFriend(string rawName)
	{
		if (!_socialService.AddFriend(rawName, out var normalizedName))
		{
			_statusLabel.Text = "Status: enter a unique friend name";
			return;
		}

		_friendNameInput.Clear();
		RefreshFriends(normalizedName);
		_statusLabel.Text = $"Status: added friend '{normalizedName}'";
	}

	private void RemoveSelectedFriend()
	{
		var selectedFriend = GetSelectedFriend();
		if (string.IsNullOrWhiteSpace(selectedFriend))
		{
			_statusLabel.Text = "Status: select a friend to remove";
			return;
		}

		if (_socialService.RemoveFriend(selectedFriend))
		{
			RefreshFriends();
			_statusLabel.Text = $"Status: removed '{selectedFriend}'";
		}
	}

	private void RefreshFriends(string? selectedFriend = null)
	{
		var friends = new List<string>();
		foreach (var friend in _socialService.GetFriends())
		{
			friends.Add(friend);
		}

		_friendsSource.DataSource = friends;

		if (friends.Count == 0)
		{
			_conversationTitleLabel.Text = "Select a friend to start messaging";
			_messagesPanel.Controls.Clear();
			UpdateInteractionState();
			return;
		}

		var friendToSelect = selectedFriend;
		var selectedFriendExists =
			!string.IsNullOrWhiteSpace(friendToSelect) &&
			ContainsAlgorithm.Run(
				friends,
				friend => string.Equals(friend, friendToSelect, StringComparison.OrdinalIgnoreCase));

		if (string.IsNullOrWhiteSpace(friendToSelect) || !selectedFriendExists)
		{
			friendToSelect = friends[0];
		}

		if (TryFindFirstAlgorithm.Run(
			friends,
			friend => string.Equals(friend, friendToSelect, StringComparison.OrdinalIgnoreCase),
			out var selectedFriendItem))
		{
			_friendsList.SelectedItem = selectedFriendItem;
		}

		RefreshConversation();
	}

	private void RefreshConversation()
	{
		var selectedFriend = GetSelectedFriend();
		_messagesPanel.SuspendLayout();
		try
		{
			_messagesPanel.Controls.Clear();
			if (!string.IsNullOrWhiteSpace(selectedFriend))
			{
				foreach (var message in _socialService.GetMessages(selectedFriend))
				{
					_messagesPanel.Controls.Add(BuildMessageCard(message));
				}
			}
		}
		finally
		{
			_messagesPanel.ResumeLayout();
		}

		_conversationTitleLabel.Text = string.IsNullOrWhiteSpace(selectedFriend)
			? "Select a friend to start messaging"
			: $"Conversation with {selectedFriend}";

		ResizeMessageCards();
		ScrollMessagesToBottom();
		UpdateInteractionState();
	}

	private void SendMessage()
	{
		var selectedFriend = GetSelectedFriend();
		if (string.IsNullOrWhiteSpace(selectedFriend))
		{
			_statusLabel.Text = "Status: add and select a friend first";
			return;
		}

		var message = _socialService.SendTextMessage(selectedFriend, _messageInput.Text);
		if (message == null)
		{
			_statusLabel.Text = "Status: write a message first";
			return;
		}

		_messageInput.Clear();
		RefreshConversation();
		_statusLabel.Text = $"Status: message sent to {selectedFriend}";
	}

	private void HandleMessageInputKeyDown(object? sender, KeyEventArgs e)
	{
		if (e.KeyCode != Keys.Enter || e.Modifiers != Keys.None)
		{
			return;
		}

		e.SuppressKeyPress = true;
		SendMessage();
	}

	private void OpenSelectedFriendConversation()
	{
		var selectedFriend = GetSelectedFriend();
		if (string.IsNullOrWhiteSpace(selectedFriend))
		{
			_statusLabel.Text = "Status: select a friend first";
			return;
		}

		RefreshConversation();
		_splitContainer.Panel2Collapsed = false;
		_splitContainer.Panel1Collapsed = true;
		_backButton.Visible = true;
		_messageInput.Focus();
		_statusLabel.Text = $"Status: opened chat with {selectedFriend}";
	}

	private void ReturnToProfiles()
	{
		ShowProfilesOnly();
		_statusLabel.Text = "Status: browsing friends";
	}

	private void OpenSharedClip(SocialMessage message)
	{
		if (!string.Equals(message.Kind, SocialMessageKinds.Clip, StringComparison.OrdinalIgnoreCase) ||
			string.IsNullOrWhiteSpace(message.ClipPath))
		{
			_statusLabel.Text = "Status: clip is unavailable";
			return;
		}

		if (!File.Exists(message.ClipPath))
		{
			_statusLabel.Text = "Status: shared clip file is missing";
			return;
		}

		Process.Start(new ProcessStartInfo(message.ClipPath) { UseShellExecute = true });
		_statusLabel.Text = $"Status: opened {Path.GetFileName(message.ClipPath)}";
	}

	private string GetSelectedFriend()
	{
		return _friendsList.SelectedItem as string ?? string.Empty;
	}

	private void UpdateInteractionState()
	{
		var hasFriend = !string.IsNullOrWhiteSpace(GetSelectedFriend());
		_sendButton.Enabled = hasFriend;
		_messageInput.Enabled = hasFriend;
		_backButton.Visible = _splitContainer.Panel1Collapsed && !_splitContainer.Panel2Collapsed && hasFriend;
	}

	private void ShowProfilesOnly()
	{
		_splitContainer.Panel1Collapsed = false;
		_splitContainer.Panel2Collapsed = true;
		_backButton.Visible = false;
		_friendsList.Focus();
	}

	private Control BuildMessageCard(SocialMessage message)
	{
		var isClip = string.Equals(message.Kind, SocialMessageKinds.Clip, StringComparison.OrdinalIgnoreCase);
		var card = new Panel
		{
			AutoSize = true,
			BackColor = isClip ? ClipBubbleColor : UiTheme.AccentColor,
			Padding = new Padding(12),
			Margin = new Padding(0, 0, 0, 10),
			Tag = message
		};

		var layout = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			AutoSize = true,
			ColumnCount = 1,
			BackColor = Color.Transparent
		};

		var metaLabel = new Label
		{
			AutoSize = true,
			ForeColor = isClip ? UiTheme.SecondaryTextColor : Color.FromArgb(225, 235, 255),
			Text = $"{message.Author} - {message.CreatedAtUtc.ToLocalTime():HH:mm}"
		};

		var bodyLabel = new Label
		{
			AutoSize = true,
			MaximumSize = new Size(420, 0),
			ForeColor = isClip ? Color.FromArgb(33, 40, 48) : Color.White,
			Font = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point),
			Text = message.Body
		};

		layout.Controls.Add(metaLabel, 0, 0);

		if (isClip)
		{
			var clipNameLabel = new Label
			{
				AutoSize = true,
				MaximumSize = new Size(420, 0),
				ForeColor = Color.FromArgb(33, 40, 48),
				Font = new Font("Segoe UI Semibold", 10f, FontStyle.Regular, GraphicsUnit.Point),
				Text = string.IsNullOrWhiteSpace(message.ClipFileName) ? "Shared clip" : message.ClipFileName
			};

			var openButton = new Button
			{
				AutoSize = true,
				Text = "Open Clip"
			};
			UiTheme.StyleSecondaryButton(openButton);
			openButton.Click += (_, _) => OpenSharedClip(message);

			layout.Controls.Add(clipNameLabel, 0, 1);
			layout.Controls.Add(bodyLabel, 0, 2);
			layout.Controls.Add(openButton, 0, 3);
		}
		else
		{
			layout.Controls.Add(bodyLabel, 0, 1);
		}

		card.Controls.Add(layout);
		return card;
	}

	private void ResizeMessageCards()
	{
		if (_messagesPanel.IsDisposed)
		{
			return;
		}

		var cardWidth = Math.Max(260, _messagesPanel.ClientSize.Width - 36);
		foreach (Control control in _messagesPanel.Controls)
		{
			control.Width = cardWidth;
		}
	}

	private void ScrollMessagesToBottom()
	{
		if (_messagesPanel.Controls.Count == 0)
		{
			return;
		}

		_messagesPanel.ScrollControlIntoView(_messagesPanel.Controls[_messagesPanel.Controls.Count - 1]);
	}

}
