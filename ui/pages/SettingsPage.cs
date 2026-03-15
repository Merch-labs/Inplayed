using System.IO;
using System.Windows.Forms;

namespace inplayed;

public sealed class SettingsPage : UserControl
{
	private readonly CheckBox _nativeNvencCheckBox;
	private readonly NumericUpDown _fpsInput;
	private readonly NumericUpDown _bitrateInput;
	private readonly NumericUpDown _clipSecondsInput;
	private readonly CheckBox _includeMicAudioCheckBox;
	private readonly CheckBox _includeSystemAudioCheckBox;
	private readonly CheckBox _ctrlModifierCheckBox;
	private readonly CheckBox _altModifierCheckBox;
	private readonly CheckBox _shiftModifierCheckBox;
	private readonly CheckBox _winModifierCheckBox;
	private readonly ComboBox _hotkeyComboBox;
	private readonly Label _configPathLabel;
	private readonly Label _statusLabel;
	private readonly Action? _onSettingsSaved;

	public SettingsPage(Action? onSettingsSaved = null)
	{
		_onSettingsSaved = onSettingsSaved;

		var root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			RowCount = 6,
			ColumnCount = 1,
			Padding = new Padding(12)
		};
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

		_nativeNvencCheckBox = new CheckBox
		{
			AutoSize = true,
			Text = "Enable native NVENC (if available)"
		};

		var recordingGroup = new GroupBox
		{
			AutoSize = true,
			Dock = DockStyle.Top,
			Text = "Recording",
			Padding = new Padding(10)
		};

		var recordingLayout = new TableLayoutPanel
		{
			AutoSize = true,
			ColumnCount = 2,
			RowCount = 5,
			Dock = DockStyle.Fill
		};
		recordingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		recordingLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

		_fpsInput = new NumericUpDown
		{
			Minimum = 24,
			Maximum = 240,
			Value = 60,
			Width = 90
		};

		_bitrateInput = new NumericUpDown
		{
			Minimum = 1,
			Maximum = 200,
			Value = 12,
			Width = 90
		};

		_clipSecondsInput = new NumericUpDown
		{
			Minimum = 5,
			Maximum = 300,
			Value = 20,
			Width = 90
		};

		_includeMicAudioCheckBox = new CheckBox
		{
			AutoSize = true,
			Text = "Include microphone audio"
		};

		_includeSystemAudioCheckBox = new CheckBox
		{
			AutoSize = true,
			Text = "Include system audio"
		};

		recordingLayout.Controls.Add(new Label { AutoSize = true, Text = "FPS", Margin = new Padding(0, 7, 8, 0) }, 0, 0);
		recordingLayout.Controls.Add(_fpsInput, 1, 0);
		recordingLayout.Controls.Add(new Label { AutoSize = true, Text = "Bitrate (Mbps)", Margin = new Padding(0, 7, 8, 0) }, 0, 1);
		recordingLayout.Controls.Add(_bitrateInput, 1, 1);
		recordingLayout.Controls.Add(new Label { AutoSize = true, Text = "Clip Length (sec)", Margin = new Padding(0, 7, 8, 0) }, 0, 2);
		recordingLayout.Controls.Add(_clipSecondsInput, 1, 2);
		recordingLayout.Controls.Add(_includeMicAudioCheckBox, 0, 3);
		recordingLayout.SetColumnSpan(_includeMicAudioCheckBox, 2);
		recordingLayout.Controls.Add(_includeSystemAudioCheckBox, 0, 4);
		recordingLayout.SetColumnSpan(_includeSystemAudioCheckBox, 2);
		recordingGroup.Controls.Add(recordingLayout);

		var hotkeyGroup = new GroupBox
		{
			AutoSize = true,
			Dock = DockStyle.Top,
			Text = "Save Clip Hotkey",
			Padding = new Padding(10)
		};

		var hotkeyLayout = new FlowLayoutPanel
		{
			AutoSize = true,
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = true
		};

		_ctrlModifierCheckBox = new CheckBox { AutoSize = true, Text = "Ctrl" };
		_altModifierCheckBox = new CheckBox { AutoSize = true, Text = "Alt" };
		_shiftModifierCheckBox = new CheckBox { AutoSize = true, Text = "Shift" };
		_winModifierCheckBox = new CheckBox { AutoSize = true, Text = "Win" };

		var hotkeyLabel = new Label
		{
			AutoSize = true,
			Text = "Key:",
			Margin = new Padding(14, 6, 0, 0)
		};

		_hotkeyComboBox = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Width = 90
		};
		_hotkeyComboBox.Items.AddRange(GetKeyOptions());

		hotkeyLayout.Controls.Add(_ctrlModifierCheckBox);
		hotkeyLayout.Controls.Add(_altModifierCheckBox);
		hotkeyLayout.Controls.Add(_shiftModifierCheckBox);
		hotkeyLayout.Controls.Add(_winModifierCheckBox);
		hotkeyLayout.Controls.Add(hotkeyLabel);
		hotkeyLayout.Controls.Add(_hotkeyComboBox);
		hotkeyGroup.Controls.Add(hotkeyLayout);

		_configPathLabel = new Label
		{
			AutoSize = true
		};

		var actions = new FlowLayoutPanel
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false
		};

		var saveButton = new Button { Text = "Save", AutoSize = true };
		var reloadButton = new Button { Text = "Reload", AutoSize = true };
		var defaultsButton = new Button { Text = "Reset Defaults", AutoSize = true };
		var openConfigButton = new Button { Text = "Open Config File", AutoSize = true };

		saveButton.Click += (_, _) => SaveSettings();
		reloadButton.Click += (_, _) => LoadSettings();
		defaultsButton.Click += (_, _) => LoadDefaults();
		openConfigButton.Click += (_, _) => OpenConfigFile();

		actions.Controls.Add(saveButton);
		actions.Controls.Add(reloadButton);
		actions.Controls.Add(defaultsButton);
		actions.Controls.Add(openConfigButton);

		_statusLabel = new Label
		{
			AutoSize = true,
			Text = "Status: ready"
		};

		root.Controls.Add(_nativeNvencCheckBox, 0, 0);
		root.Controls.Add(recordingGroup, 0, 1);
		root.Controls.Add(hotkeyGroup, 0, 2);
		root.Controls.Add(_configPathLabel, 0, 3);
		root.Controls.Add(actions, 0, 4);
		root.Controls.Add(_statusLabel, 0, 5);

		Controls.Add(root);
		LoadSettings();
	}

	private void LoadSettings()
	{
		var config = AppConfig.Load();
		LoadFromConfig(config);
		_configPathLabel.Text = $"Config: {AppConfig.GetConfigPath()}";
		_statusLabel.Text = "Status: loaded config";
	}

	private void LoadDefaults()
	{
		LoadFromConfig(AppConfig.CreateDefault());
		_statusLabel.Text = "Status: loaded defaults (not saved)";
	}

	private void LoadFromConfig(AppConfig config)
	{
		_nativeNvencCheckBox.Checked = config.NativeNvencEnabled ?? true;
		_fpsInput.Value = config.Recording.Fps;
		_bitrateInput.Value = config.Recording.BitrateMbps;
		_clipSecondsInput.Value = config.Recording.ClipSeconds;
		_includeMicAudioCheckBox.Checked = config.Recording.IncludeMicAudio;
		_includeSystemAudioCheckBox.Checked = config.Recording.IncludeSystemAudio;
		ApplyModifiers(config.SaveClipHotkey.Modifiers);
		SelectHotkeyKey(config.SaveClipHotkey.Key);
	}

	private void SaveSettings()
	{
		if (_hotkeyComboBox.SelectedItem is not string key || string.IsNullOrWhiteSpace(key))
		{
			_statusLabel.Text = "Status: choose a hotkey key";
			return;
		}

		var config = new AppConfig
		{
			NativeNvencEnabled = _nativeNvencCheckBox.Checked,
			SaveClipHotkey = new AppConfig.HotkeyConfig
			{
				Modifiers = BuildModifiers(),
				Key = key
			},
			Recording = new AppConfig.RecordingConfig
			{
				Fps = (int)_fpsInput.Value,
				BitrateMbps = (int)_bitrateInput.Value,
				ClipSeconds = (int)_clipSecondsInput.Value,
				IncludeMicAudio = _includeMicAudioCheckBox.Checked,
				IncludeSystemAudio = _includeSystemAudioCheckBox.Checked
			}
		};

		AppConfig.Save(config);
		_configPathLabel.Text = $"Config: {AppConfig.GetConfigPath()}";
		_statusLabel.Text = "Status: saved";
		_onSettingsSaved?.Invoke();
	}

	private void OpenConfigFile()
	{
		var path = AppConfig.GetConfigPath();
		if (!File.Exists(path))
		{
			AppConfig.Save(AppConfig.Load());
		}

		System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
	}

	private void ApplyModifiers(string modifiers)
	{
		var values = modifiers
			.Split(new[] { '+', '|', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
			.Select(v => v.Trim())
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		_ctrlModifierCheckBox.Checked = values.Contains("Control") || values.Contains("Ctrl");
		_altModifierCheckBox.Checked = values.Contains("Alt");
		_shiftModifierCheckBox.Checked = values.Contains("Shift");
		_winModifierCheckBox.Checked = values.Contains("Windows") || values.Contains("Win");
	}

	private void SelectHotkeyKey(string key)
	{
		var idx = _hotkeyComboBox.Items.IndexOf(key.ToUpperInvariant());
		_hotkeyComboBox.SelectedIndex = idx >= 0 ? idx : 0;
	}

	private string BuildModifiers()
	{
		var parts = new List<string>();
		if (_ctrlModifierCheckBox.Checked) parts.Add("Control");
		if (_altModifierCheckBox.Checked) parts.Add("Alt");
		if (_shiftModifierCheckBox.Checked) parts.Add("Shift");
		if (_winModifierCheckBox.Checked) parts.Add("Windows");

		return parts.Count > 0 ? string.Join("+", parts) : "Alt";
	}

	private static object[] GetKeyOptions()
	{
		var keys = new List<object>();
		for (var i = 1; i <= 12; i++)
		{
			keys.Add($"F{i}");
		}

		for (var c = 'A'; c <= 'Z'; c++)
		{
			keys.Add(c.ToString());
		}

		for (var i = 0; i <= 9; i++)
		{
			keys.Add($"D{i}");
		}

		return keys.ToArray();
	}
}
