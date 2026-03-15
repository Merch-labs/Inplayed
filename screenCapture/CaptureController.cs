using System.Drawing;
using System.Windows.Forms;

namespace inplayed;

public sealed class CaptureController : IDisposable
{
	private readonly object _sync = new();
	private readonly FfmpegMediaMuxer _mediaMuxer = new();
	private ClipSession? _session;
	private string _lastSessionStatus = "stopped";
	private readonly AudioRecorder _micRecorder = new();
	private readonly AudioRecorder _systemRecorder = new();
	private bool _disposed;
	private bool _sessionStarting;
	private Action<Bitmap>? _previewFrameUpdated;
	private int _previewSubscriberCount;

	public event Action<Bitmap> PreviewFrameUpdated
	{
		add
		{
			lock (_sync)
			{
				_previewFrameUpdated += value;
				_previewSubscriberCount++;
				UpdatePreviewStateLocked();
			}
		}
		remove
		{
			lock (_sync)
			{
				_previewFrameUpdated -= value;
				_previewSubscriberCount = Math.Max(0, _previewSubscriberCount - 1);
				UpdatePreviewStateLocked();
			}
		}
	}

	public Task StartCapture()
	{
		ThrowIfDisposed();
		lock (_sync)
		{
			if (_session != null || _sessionStarting)
			{
				return Task.CompletedTask;
			}

			_sessionStarting = true;
			var settings = CreateDefaultSettings();
			var session = new ClipSession(settings);
			session.StatusChanged += OnSessionStatusChanged;
			session.PreviewFrameReady += OnPreviewFrameReady;
			var appConfig = AppConfig.Load();
			StartAudioRecording(
				settings.ClipSeconds,
				appConfig.Recording.IncludeMicAudio,
				appConfig.Recording.IncludeSystemAudio);
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
				UpdatePreviewStateLocked();
			}
		}
		catch
		{
			session.StatusChanged -= OnSessionStatusChanged;
			session.PreviewFrameReady -= OnPreviewFrameReady;
			session.Dispose();
			await StopAudioRecording();
			throw;
		}
		finally
		{
			lock (_sync)
			{
				_sessionStarting = false;
			}
		}
	}

	public async Task StopCapture()
	{
		if (_disposed)
		{
			return;
		}

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
			session.PreviewFrameReady -= OnPreviewFrameReady;
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

	public Task StartAudioRecording(int clipSeconds, bool includeMicAudio, bool includeSystemAudio)
	{
		ThrowIfDisposed();
		if (includeMicAudio)
		{
			_micRecorder.StartMic(clipSeconds);
		}

		if (includeSystemAudio)
		{
			_systemRecorder.StartSystem(clipSeconds);
		}

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
		var saveTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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
		var tempVideoPath = GetTemporaryVideoPath(outputPath);
		await session.SaveClipAsync(tempVideoPath, saveTimestampMs);
		if (!System.IO.File.Exists(tempVideoPath))
		{
			OnSessionStatusChanged("save_failed:no_video_file");
			return;
		}

		var (micPath, systemPath) = SaveAudioClip(outputPath, saveTimestampMs);
		try
		{
			if (!string.IsNullOrWhiteSpace(micPath) || !string.IsNullOrWhiteSpace(systemPath))
			{
				await _mediaMuxer.MuxAsync(tempVideoPath, outputPath, micPath, systemPath);
				TryDeleteFile(tempVideoPath);
				TryDeleteFile(micPath);
				TryDeleteFile(systemPath);
				OnSessionStatusChanged($"save_ok:{outputPath}:muxed");
				return;
			}

			System.IO.File.Move(tempVideoPath, outputPath, overwrite: true);
			OnSessionStatusChanged($"save_ok:{outputPath}");
		}
		catch (Exception ex)
		{
			System.IO.File.Move(tempVideoPath, outputPath, overwrite: true);
			OnSessionStatusChanged($"save_partial:{outputPath}:audio_mux_failed:{ex.GetType().Name}");
		}
	}

	private (string? MicPath, string? SystemPath) SaveAudioClip(string outputPath, long endTimestampMs)
	{
		var appConfig = AppConfig.Load();
		var baseFolder = GetDefaultMediaFolder();
		var clipStem = System.IO.Path.GetFileNameWithoutExtension(outputPath);
		var micPath = System.IO.Path.Combine(baseFolder, $"{clipStem}.mic.wav");
		var systemPath = System.IO.Path.Combine(baseFolder, $"{clipStem}.system.wav");
		var savedMicPath = appConfig.Recording.IncludeMicAudio ? _micRecorder.SaveClip(micPath, endTimestampMs) : null;
		var savedSystemPath = appConfig.Recording.IncludeSystemAudio ? _systemRecorder.SaveClip(systemPath, endTimestampMs) : null;
		return (savedMicPath, savedSystemPath);
	}

	private static RecordingSettings CreateDefaultSettings()
	{
		var appConfig = AppConfig.Load();
		return RecordingSettingsFactory.Create(appConfig);
	}

	private static string GetDefaultClipPath()
	{
		var folder = GetDefaultMediaFolder();

		var fileName = $"clip_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
		return System.IO.Path.Combine(folder, fileName);
	}

	private static string GetTemporaryVideoPath(string finalOutputPath)
	{
		var folder = System.IO.Path.GetDirectoryName(finalOutputPath) ?? GetDefaultMediaFolder();
		var stem = System.IO.Path.GetFileNameWithoutExtension(finalOutputPath);
		return System.IO.Path.Combine(folder, $"{stem}.{Guid.NewGuid():N}.video.tmp.mp4");
	}

	private static string GetDefaultMediaFolder()
	{
		var folder = System.IO.Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
			"inplayed");
		System.IO.Directory.CreateDirectory(folder);
		return folder;
	}

	private static void TryDeleteFile(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}

		try
		{
			if (System.IO.File.Exists(path))
			{
				System.IO.File.Delete(path);
			}
		}
		catch
		{
		}
	}

	private void OnSessionStatusChanged(string status)
	{
		lock (_sync)
		{
			_lastSessionStatus = status;
		}

		Console.WriteLine($"Session status: {status}");
	}

	private void OnPreviewFrameReady(Bitmap frame)
	{
		var handler = _previewFrameUpdated;
		if (handler != null)
		{
			handler(frame);
			return;
		}

		frame.Dispose();
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		try
		{
			StopCapture().GetAwaiter().GetResult();
		}
		catch
		{
		}

		_micRecorder.Dispose();
		_systemRecorder.Dispose();
		_disposed = true;
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(CaptureController));
		}
	}

	private void UpdatePreviewStateLocked()
	{
		_session?.SetPreviewEnabled(_previewSubscriberCount > 0);
	}
}
