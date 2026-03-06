using System.Drawing;
using System.Windows.Forms;

namespace inplayed;

public sealed class RecordingPage : UserControl
{
	private readonly CaptureController _controller;
	private readonly PictureBox _previewBox;
	private readonly Label _statusLabel;
	private readonly Button _startButton;
	private readonly Button _stopButton;
	private readonly Button _saveButton;
	private readonly System.Windows.Forms.Timer _previewTimer;

	public RecordingPage(CaptureController controller)
	{
		_controller = controller;

		var root = new TableLayoutPanel
		{
			Dock = DockStyle.Fill,
			RowCount = 3,
			ColumnCount = 1
		};
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
		root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
		root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

		_statusLabel = new Label
		{
			Dock = DockStyle.Top,
			AutoSize = true,
			Padding = new Padding(12, 12, 12, 8),
			Text = "Status: stopped"
		};

		_previewBox = new PictureBox
		{
			Dock = DockStyle.Fill,
			SizeMode = PictureBoxSizeMode.Zoom,
			BackColor = Color.Black
		};

		var actions = new FlowLayoutPanel
		{
			Dock = DockStyle.Fill,
			FlowDirection = FlowDirection.LeftToRight,
			WrapContents = false,
			Padding = new Padding(12, 8, 12, 12),
			AutoSize = true
		};

		_startButton = new Button { Text = "Start Capture", AutoSize = true };
		_stopButton = new Button { Text = "Stop Capture", AutoSize = true };
		_saveButton = new Button { Text = "Save Clip", AutoSize = true };

		_startButton.Click += async (_, _) => await StartCaptureAsync();
		_stopButton.Click += async (_, _) => await StopCaptureAsync();
		_saveButton.Click += async (_, _) => await SaveClipAsync();

		actions.Controls.Add(_startButton);
		actions.Controls.Add(_stopButton);
		actions.Controls.Add(_saveButton);

		root.Controls.Add(_statusLabel, 0, 0);
		root.Controls.Add(_previewBox, 0, 1);
		root.Controls.Add(actions, 0, 2);
		Controls.Add(root);

		_previewTimer = new System.Windows.Forms.Timer { Interval = 33 };
		_previewTimer.Tick += (_, _) => RefreshPreview();
		_previewTimer.Start();

		UpdateStatus();
	}

	private async Task StartCaptureAsync()
	{
		try
		{
			await _controller.StartCapture();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, ex.Message, "Start capture failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		UpdateStatus();
	}

	private async Task StopCaptureAsync()
	{
		try
		{
			await _controller.StopCapture();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, ex.Message, "Stop capture failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		UpdateStatus();
	}

	private async Task SaveClipAsync()
	{
		try
		{
			await _controller.SaveClip();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, ex.Message, "Save clip failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
		}

		UpdateStatus();
	}

	private void RefreshPreview()
	{
		var preview = _controller.GetPreviewFrame(960, 540);
		if (preview == null)
		{
			return;
		}

		var previous = _previewBox.Image;
		_previewBox.Image = preview;
		previous?.Dispose();
		UpdateStatus();
	}

	private void UpdateStatus()
	{
		_statusLabel.Text = $"Status: {_controller.GetSessionStatus()}";
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			_previewTimer.Stop();
			_previewTimer.Dispose();
			_previewBox.Image?.Dispose();
		}

		base.Dispose(disposing);
	}
}
