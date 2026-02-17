using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;

namespace inplayed;

using Microsoft.Web.WebView2.Core;

public partial class MainWindow : Window
{
	public MainWindow()
	{
		InitializeComponent();
		Loaded += MainWindow_Loaded;
	}

	private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
	{
		await webView.EnsureCoreWebView2Async();

		var path = System.IO.Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"ui/index.html"
		);

		webView.Source = new Uri(path);

		webView.CoreWebView2.AddHostObjectToScript(
			"backend",
			new Backend()
		);
	}
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.AutoDual)]
public class Backend
{
	private readonly object _sync = new();
	private ClipSession? _session;
	private string _lastSessionStatus = "stopped";
	private readonly AudioRecorder _micRecorder = new();
	private readonly AudioRecorder _systemRecorder = new();

	public Task StartCapture()
	{
		lock (_sync)
		{
			if (_session != null)
			{
				return Task.CompletedTask;
			}

			var settings = CreateDefaultSettings();
			var session = new ClipSession(settings);
			session.StatusChanged += OnSessionStatusChanged;
			StartAudioRecording(settings.ClipSeconds);
			return StartSessionAsync(session);
		}
	}

	private async Task StartSessionAsync(ClipSession session)
	{
		try
		{
			await session.StartAsync();
			lock (_sync)
			{
				_session = session;
			}
		}
		catch
		{
			session.StatusChanged -= OnSessionStatusChanged;
			session.Dispose();
			await StopAudioRecording();
			throw;
		}
	}

	public async Task StopCapture()
	{
		ClipSession? session;
		lock (_sync)
		{
			session = _session;
			_session = null;
		}

		await StopAudioRecording();

		if (session != null)
		{
			await session.StopAsync();
			session.StatusChanged -= OnSessionStatusChanged;
			session.Dispose();
		}
	}

	public string GetSessionStatus()
	{
		lock (_sync)
		{
			return _lastSessionStatus;
		}
	}

	public string GetCaptureStats()
	{
		lock (_sync)
		{
			if (_session == null)
			{
				return "inactive";
			}

			return _session.GetDebugStatus();
		}
	}

	public string GetNvencReadiness()
	{
		var readiness = NvencHardwareEncoder.ProbeReadiness();
		return $"ready={readiness.IsReady};summary={readiness.Summary};maxVer=0x{readiness.MaxSupportedVersion:X8};cuda={readiness.CudaDriverVersion};fnPtrs={readiness.FunctionPointerCount}";
	}

	public Task StartAudioRecording(int clipSeconds)
	{
		_micRecorder.StartMic(clipSeconds);
		_systemRecorder.StartSystem(clipSeconds);

		return Task.CompletedTask;
	}

	public Task StopAudioRecording()
	{
		_micRecorder.Stop();
		_systemRecorder.Stop();
		return Task.CompletedTask;
	}

	public async Task SaveClip()
	{
		ClipSession? session;
		lock (_sync)
		{
			session = _session;
		}

		if (session == null)
		{
			return;
		}

		var outputPath = GetDefaultClipPath();
		await session.SaveClipAsync(outputPath);
		if (!System.IO.File.Exists(outputPath))
		{
			OnSessionStatusChanged("save_failed:no_video_file");
			return;
		}

		SaveAudioClip();
		OnSessionStatusChanged($"save_ok:{outputPath}");
	}

	private void SaveAudioClip()
	{
		var baseFolder = GetDefaultMediaFolder();
		var micPath = System.IO.Path.Combine(baseFolder, $"audio_mic_clip_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
		var systemPath = System.IO.Path.Combine(baseFolder, $"audio_system_clip_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
		_micRecorder.SaveClip(micPath);
		_systemRecorder.SaveClip(systemPath);
	}

	private static RecordingSettings CreateDefaultSettings()
	{
		var monitorIndex = ActiveTargetResolver.GetActiveMonitorIndex();
		var screens = Screen.AllScreens;
		var screen = (monitorIndex >= 0 && monitorIndex < screens.Length) ? screens[monitorIndex] : Screen.PrimaryScreen;
		var bounds = screen?.Bounds ?? new System.Drawing.Rectangle(0, 0, 1920, 1080);

		return new RecordingSettings
		{
			Width = bounds.Width,
			Height = bounds.Height,
			Fps = 60,
			Bitrate = 12_000_000,
			ClipSeconds = 20,
			Target = new MonitorTarget { MonitorIndex = monitorIndex }
		};
	}

	private static string GetDefaultClipPath()
	{
		var folder = GetDefaultMediaFolder();

		var fileName = $"clip_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
		return System.IO.Path.Combine(folder, fileName);
	}

	private static string GetDefaultMediaFolder()
	{
		var folder = System.IO.Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
			"inplayed");
		System.IO.Directory.CreateDirectory(folder);
		return folder;
	}

	private void OnSessionStatusChanged(string status)
	{
		lock (_sync)
		{
			_lastSessionStatus = status;
		}

		Console.WriteLine($"Session status: {status}");
	}
}
