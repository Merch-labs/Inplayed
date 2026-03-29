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
	private readonly CheckBox _enableYamnetDetectionCheckBox;
	private readonly TextBox _yamnetModelPathTextBox;
	private readonly NumericUpDown _yamnetSensitivityInput;
	private readonly NumericUpDown _yamnetCooldownInput;
	private readonly ComboBox _captureTargetModeComboBox;
	private readonly NumericUpDown _monitorIndexInput;
	private readonly TextBox _executablePathTextBox;
	private readonly CheckBox _ctrlModifierCheckBox;
	private readonly CheckBox _altModifierCheckBox;
	private readonly CheckBox _shiftModifierCheckBox;
	private readonly CheckBox _winModifierCheckBox;
	private readonly ComboBox _hotkeyComboBox;
	private readonly CheckBox _launchOnWindowsStartupCheckBox;
	private readonly CheckBox _startHiddenOnWindowsStartupCheckBox;
	private readonly CheckBox _autoStartCaptureWhenHiddenLaunchCheckBox;
	private readonly Label _configPathLabel;
	private readonly Label _statusLabel;
	private readonly Action? _onSettingsSaved;

	public SettingsPage(Action? onSettingsSaved = null)
	{
		_onSettingsSaved = onSettingsSaved;
		BackColor = UiTheme.ShellBackground;

		var root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			RowCount = 9,
			ColumnCount = 1,
			Padding = new Padding(12),
			BackColor = UiTheme.ShellBackground
		};
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
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

		var yamnetGroup = new GroupBox
		{
			AutoSize = true,
			Dock = DockStyle.Top,
			Text = "YAMNet Clipping",
			Padding = new Padding(10)
		};

		var yamnetLayout = new TableLayoutPanel
		{
			AutoSize = true,
			ColumnCount = 3,
			RowCount = 5,
			Dock = DockStyle.Fill
		};
		yamnetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		yamnetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		yamnetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

		_enableYamnetDetectionCheckBox = new CheckBox
		{
			AutoSize = true,
			Text = "Enable YAMNet-based clipping"
		};
		_enableYamnetDetectionCheckBox.CheckedChanged += (_, _) => UpdateYamnetInputs();

		_yamnetModelPathTextBox = new TextBox
		{
			Width = 340
		};

		var browseYamnetModelButton = new Button
		{
			AutoSize = true,
			Text = "Browse Model..."
		};
		UiTheme.StyleSecondaryButton(browseYamnetModelButton);
		browseYamnetModelButton.Click += (_, _) => BrowseYamnetModelPath();

		var testYamnetModelButton = new Button
		{
			AutoSize = true,
			Text = "Test Model"
		};
		UiTheme.StyleSecondaryButton(testYamnetModelButton);
		testYamnetModelButton.Click += (_, _) => ValidateYamnetModel();

		_yamnetSensitivityInput = new NumericUpDown
		{
			Minimum = 1,
			Maximum = 100,
			Value = 65,
			Width = 90
		};

		_yamnetCooldownInput = new NumericUpDown
		{
			Minimum = 5,
			Maximum = 120,
			Value = 15,
			Width = 90
		};

		var yamnetHintLabel = new Label
		{
			AutoSize = true,
			Text = "Uses an ONNX-exported YAMNet model. This pass wires config and model validation first."
		};

		yamnetLayout.Controls.Add(_enableYamnetDetectionCheckBox, 0, 0);
		yamnetLayout.SetColumnSpan(_enableYamnetDetectionCheckBox, 3);
		yamnetLayout.Controls.Add(new Label { AutoSize = true, Text = "Model Path", Margin = new Padding(0, 7, 8, 0) }, 0, 1);
		yamnetLayout.Controls.Add(_yamnetModelPathTextBox, 1, 1);
		yamnetLayout.Controls.Add(browseYamnetModelButton, 2, 1);
		yamnetLayout.Controls.Add(testYamnetModelButton, 2, 2);
		yamnetLayout.Controls.Add(new Label { AutoSize = true, Text = "Sensitivity", Margin = new Padding(0, 7, 8, 0) }, 0, 3);
		yamnetLayout.Controls.Add(_yamnetSensitivityInput, 1, 3);
		yamnetLayout.Controls.Add(new Label { AutoSize = true, Text = "Cooldown (sec)", Margin = new Padding(0, 7, 8, 0) }, 0, 4);
		yamnetLayout.Controls.Add(_yamnetCooldownInput, 1, 4);
		yamnetLayout.Controls.Add(yamnetHintLabel, 0, 5);
		yamnetLayout.SetColumnSpan(yamnetHintLabel, 3);
		yamnetGroup.Controls.Add(yamnetLayout);

		var captureTargetGroup = new GroupBox
		{
			AutoSize = true,
			Dock = DockStyle.Top,
			Text = "Capture Target",
			Padding = new Padding(10)
		};

		var captureTargetLayout = new TableLayoutPanel
		{
			AutoSize = true,
			ColumnCount = 3,
			RowCount = 3,
			Dock = DockStyle.Fill
		};
		captureTargetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		captureTargetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
		captureTargetLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

		_captureTargetModeComboBox = new ComboBox
		{
			DropDownStyle = ComboBoxStyle.DropDownList,
			Width = 220
		};
		_captureTargetModeComboBox.Items.AddRange(GetCaptureTargetOptions());
		_captureTargetModeComboBox.SelectedIndexChanged += (_, _) => UpdateCaptureTargetInputs();

		_monitorIndexInput = new NumericUpDown
		{
			Minimum = 0,
			Maximum = Math.Max(0, Screen.AllScreens.Length - 1),
			Value = 0,
			Width = 90
		};

		_executablePathTextBox = new TextBox
		{
			Width = 340
		};

		var browseExecutableButton = new Button
		{
			AutoSize = true,
			Text = "Browse..."
		};
		UiTheme.StyleSecondaryButton(browseExecutableButton);
		browseExecutableButton.Click += (_, _) => BrowseExecutablePath();

		captureTargetLayout.Controls.Add(new Label { AutoSize = true, Text = "Mode", Margin = new Padding(0, 7, 8, 0) }, 0, 0);
		captureTargetLayout.Controls.Add(_captureTargetModeComboBox, 1, 0);
		captureTargetLayout.SetColumnSpan(_captureTargetModeComboBox, 2);
		captureTargetLayout.Controls.Add(new Label { AutoSize = true, Text = "Monitor Index", Margin = new Padding(0, 7, 8, 0) }, 0, 1);
		captureTargetLayout.Controls.Add(_monitorIndexInput, 1, 1);
		captureTargetLayout.Controls.Add(new Label { AutoSize = true, Text = "Executable Path", Margin = new Padding(0, 7, 8, 0) }, 0, 2);
		captureTargetLayout.Controls.Add(_executablePathTextBox, 1, 2);
		captureTargetLayout.Controls.Add(browseExecutableButton, 2, 2);
		captureTargetGroup.Controls.Add(captureTargetLayout);

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

		var startupGroup = new GroupBox
		{
			AutoSize = true,
			Dock = DockStyle.Top,
			Text = "Startup And Background",
			Padding = new Padding(10)
		};

		var startupLayout = new FlowLayoutPanel
		{
			AutoSize = true,
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.TopDown,
			WrapContents = false
		};

		_launchOnWindowsStartupCheckBox = new CheckBox
		{
			AutoSize = true,
			Text = "Launch inplayed when I sign in to Windows"
		};
		_startHiddenOnWindowsStartupCheckBox = new CheckBox
		{
			AutoSize = true,
			Text = "Launch hidden in the background"
		};
		_autoStartCaptureWhenHiddenLaunchCheckBox = new CheckBox
		{
			AutoSize = true,
			Text = "Auto-start capture on hidden launch"
		};

		_launchOnWindowsStartupCheckBox.CheckedChanged += (_, _) => UpdateStartupInputs();
		_startHiddenOnWindowsStartupCheckBox.CheckedChanged += (_, _) => UpdateStartupInputs();

		startupLayout.Controls.Add(_launchOnWindowsStartupCheckBox);
		startupLayout.Controls.Add(_startHiddenOnWindowsStartupCheckBox);
		startupLayout.Controls.Add(_autoStartCaptureWhenHiddenLaunchCheckBox);
		startupGroup.Controls.Add(startupLayout);

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
		UiTheme.StyleAccentButton(saveButton);
		UiTheme.StyleSecondaryButton(reloadButton);
		UiTheme.StyleSecondaryButton(defaultsButton);
		UiTheme.StyleSecondaryButton(openConfigButton);

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
		root.Controls.Add(yamnetGroup, 0, 2);
		root.Controls.Add(captureTargetGroup, 0, 3);
		root.Controls.Add(hotkeyGroup, 0, 4);
		root.Controls.Add(startupGroup, 0, 5);
		root.Controls.Add(_configPathLabel, 0, 6);
		root.Controls.Add(actions, 0, 7);
		root.Controls.Add(_statusLabel, 0, 8);

		Controls.Add(root);
		UiTheme.ApplyPalette(this);
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
		_enableYamnetDetectionCheckBox.Checked = config.Recording.YamnetDetection.Enabled;
		_yamnetModelPathTextBox.Text = config.Recording.YamnetDetection.ModelPath;
		_yamnetSensitivityInput.Value = config.Recording.YamnetDetection.SensitivityPercent;
		_yamnetCooldownInput.Value = config.Recording.YamnetDetection.CooldownSeconds;
		UpdateYamnetInputs();
		SelectCaptureTargetMode(config.Recording.CaptureTarget.Mode);
		_monitorIndexInput.Value = Math.Max(_monitorIndexInput.Minimum, Math.Min(_monitorIndexInput.Maximum, config.Recording.CaptureTarget.MonitorIndex));
		_executablePathTextBox.Text = config.Recording.CaptureTarget.ExecutablePath;
		UpdateCaptureTargetInputs();
		ApplyModifiers(config.SaveClipHotkey.Modifiers);
		SelectHotkeyKey(config.SaveClipHotkey.Key);
		_launchOnWindowsStartupCheckBox.Checked = config.Startup.LaunchOnWindowsStartup;
		_startHiddenOnWindowsStartupCheckBox.Checked = config.Startup.StartHiddenOnWindowsStartup;
		_autoStartCaptureWhenHiddenLaunchCheckBox.Checked = config.Startup.AutoStartCaptureWhenHiddenLaunch;
		UpdateStartupInputs();
	}

	private void SaveSettings()
	{
		if (_hotkeyComboBox.SelectedItem is not string key || string.IsNullOrWhiteSpace(key))
		{
			_statusLabel.Text = "Status: choose a hotkey key";
			return;
		}

		if (_enableYamnetDetectionCheckBox.Checked)
		{
			var yamnetValidation = YamnetModelValidator.Validate(_yamnetModelPathTextBox.Text.Trim());
			if (!yamnetValidation.IsValid)
			{
				_statusLabel.Text = $"Status: {yamnetValidation.Message}";
				return;
			}
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
				IncludeSystemAudio = _includeSystemAudioCheckBox.Checked,
				YamnetDetection = new AppConfig.YamnetDetectionConfig
				{
					Enabled = _enableYamnetDetectionCheckBox.Checked,
					ModelPath = _yamnetModelPathTextBox.Text.Trim(),
					SensitivityPercent = (int)_yamnetSensitivityInput.Value,
					CooldownSeconds = (int)_yamnetCooldownInput.Value
				},
				CaptureTarget = new AppConfig.CaptureTargetConfig
				{
					Mode = GetSelectedCaptureTargetMode(),
					MonitorIndex = (int)_monitorIndexInput.Value,
					ExecutablePath = _executablePathTextBox.Text.Trim()
				}
			},
			Startup = new AppConfig.StartupConfig
			{
				LaunchOnWindowsStartup = _launchOnWindowsStartupCheckBox.Checked,
				StartHiddenOnWindowsStartup = _startHiddenOnWindowsStartupCheckBox.Checked,
				AutoStartCaptureWhenHiddenLaunch = _autoStartCaptureWhenHiddenLaunchCheckBox.Checked
			}
		};

		AppConfig.Save(config);
		if (!WindowsStartupRegistration.TryApply(config.Startup, out var startupError))
		{
			_configPathLabel.Text = $"Config: {AppConfig.GetConfigPath()}";
			_statusLabel.Text = $"Status: saved, but Windows startup failed: {startupError}";
			_onSettingsSaved?.Invoke();
			return;
		}

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

	private void BrowseExecutablePath()
	{
		using var dialog = new OpenFileDialog
		{
			Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
			Title = "Select executable to capture"
		};

		if (dialog.ShowDialog(this) == DialogResult.OK)
		{
			_executablePathTextBox.Text = dialog.FileName;
		}
	}

	private void BrowseYamnetModelPath()
	{
		using var dialog = new OpenFileDialog
		{
			Filter = "ONNX model (*.onnx)|*.onnx|All files (*.*)|*.*",
			Title = "Select a YAMNet ONNX model"
		};

		if (dialog.ShowDialog(this) == DialogResult.OK)
		{
			_yamnetModelPathTextBox.Text = dialog.FileName;
		}
	}

	private void ValidateYamnetModel()
	{
		var result = YamnetModelValidator.Validate(_yamnetModelPathTextBox.Text.Trim());
		_statusLabel.Text = $"Status: {result.Message}";
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

	private void SelectCaptureTargetMode(string mode)
	{
		for (var i = 0; i < _captureTargetModeComboBox.Items.Count; i++)
		{
			if (_captureTargetModeComboBox.Items[i] is CaptureTargetOption option &&
				string.Equals(option.Mode, mode, StringComparison.OrdinalIgnoreCase))
			{
				_captureTargetModeComboBox.SelectedIndex = i;
				return;
			}
		}

		_captureTargetModeComboBox.SelectedIndex = 0;
	}

	private string GetSelectedCaptureTargetMode()
	{
		return _captureTargetModeComboBox.SelectedItem is CaptureTargetOption option
			? option.Mode
			: CaptureTargetModes.PrimaryMonitor;
	}

	private void UpdateCaptureTargetInputs()
	{
		var mode = GetSelectedCaptureTargetMode();
		var usesMonitorIndex = string.Equals(mode, CaptureTargetModes.SpecificMonitor, StringComparison.OrdinalIgnoreCase);
		var usesExecutablePath = string.Equals(mode, CaptureTargetModes.ExecutablePath, StringComparison.OrdinalIgnoreCase);

		_monitorIndexInput.Enabled = usesMonitorIndex;
		_executablePathTextBox.Enabled = usesExecutablePath;
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

	private void UpdateStartupInputs()
	{
		var launchOnWindowsStartup = _launchOnWindowsStartupCheckBox.Checked;
		_startHiddenOnWindowsStartupCheckBox.Enabled = launchOnWindowsStartup;
		_autoStartCaptureWhenHiddenLaunchCheckBox.Enabled =
			launchOnWindowsStartup && _startHiddenOnWindowsStartupCheckBox.Checked;
	}

	private void UpdateYamnetInputs()
	{
		var enabled = _enableYamnetDetectionCheckBox.Checked;
		_yamnetModelPathTextBox.Enabled = enabled;
		_yamnetSensitivityInput.Enabled = enabled;
		_yamnetCooldownInput.Enabled = enabled;
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

	private static object[] GetCaptureTargetOptions()
	{
		return new object[]
		{
			new CaptureTargetOption("Primary Monitor", CaptureTargetModes.PrimaryMonitor),
			new CaptureTargetOption("Specific Monitor Index", CaptureTargetModes.SpecificMonitor),
			new CaptureTargetOption("Active Window At Start", CaptureTargetModes.ActiveWindow),
			new CaptureTargetOption("Executable Path", CaptureTargetModes.ExecutablePath)
		};
	}

	private sealed class CaptureTargetOption
	{
		public CaptureTargetOption(string label, string mode)
		{
			Label = label;
			Mode = mode;
		}

		public string Label { get; }
		public string Mode { get; }

		public override string ToString()
		{
			return Label;
		}
	}
}
