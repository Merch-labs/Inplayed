using System.IO;
using System.Linq;
using System.Drawing;
using inplayed;
using NAudio.Wave;

public sealed class RecordingPipelineTests
{
	[Fact]
	public void AppConfig_CreateDefault_ProvidesRecordingDefaults()
	{
		var config = AppConfig.CreateDefault();

		Assert.True(config.NativeNvencEnabled);
		Assert.Equal(60, config.Recording.Fps);
		Assert.Equal(12, config.Recording.BitrateMbps);
		Assert.Equal(20, config.Recording.ClipSeconds);
		Assert.True(config.Recording.IncludeMicAudio);
		Assert.True(config.Recording.IncludeSystemAudio);
		Assert.Equal(CaptureTargetModes.PrimaryMonitor, config.Recording.CaptureTarget.Mode);
		Assert.Equal(0, config.Recording.CaptureTarget.MonitorIndex);
		Assert.Equal(string.Empty, config.Recording.CaptureTarget.ExecutablePath);
	}

	[Fact]
	public void AppConfig_GetSaveClipHotkey_ParsesCompositeModifiers()
	{
		var config = new AppConfig
		{
			SaveClipHotkey = new AppConfig.HotkeyConfig
			{
				Modifiers = "Control+Shift",
				Key = "F9"
			}
		};

		var (modifiers, key) = config.GetSaveClipHotkey();

		Assert.True(modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control));
		Assert.True(modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift));
		Assert.Equal(System.Windows.Input.Key.F9, key);
	}

	[Fact]
	public void MediaMuxer_BuildArguments_ReturnsNullWhenNoAudioInputsExist()
	{
		var tempDir = CreateTempDirectory();
		try
		{
			var videoPath = Path.Combine(tempDir, "clip.mp4");
			File.WriteAllBytes(videoPath, new byte[] { 0x01 });

			var args = FfmpegMediaMuxer.BuildArguments(
				videoPath,
				Path.Combine(tempDir, "out.mp4"),
				null,
				null);

			Assert.Null(args);
		}
		finally
		{
			Directory.Delete(tempDir, recursive: true);
		}
	}

	[Fact]
	public void MediaMuxer_BuildArguments_UsesDirectAudioMapWhenSingleAudioTrackExists()
	{
		var tempDir = CreateTempDirectory();
		try
		{
			var videoPath = Path.Combine(tempDir, "clip.mp4");
			var micPath = Path.Combine(tempDir, "clip.mic.wav");
			File.WriteAllBytes(videoPath, new byte[] { 0x01 });
			File.WriteAllBytes(micPath, new byte[] { 0x02 });

			var args = FfmpegMediaMuxer.BuildArguments(
				videoPath,
				Path.Combine(tempDir, "out.mp4"),
				micPath,
				null);

			Assert.NotNull(args);
			Assert.Contains("-map 0:v:0 -map 1:a:0", args);
			Assert.DoesNotContain("amix", args);
		}
		finally
		{
			Directory.Delete(tempDir, recursive: true);
		}
	}

	[Fact]
	public void MediaMuxer_BuildArguments_MixesMicAndSystemAudioWhenBothExist()
	{
		var tempDir = CreateTempDirectory();
		try
		{
			var videoPath = Path.Combine(tempDir, "clip.mp4");
			var micPath = Path.Combine(tempDir, "clip.mic.wav");
			var systemPath = Path.Combine(tempDir, "clip.system.wav");
			File.WriteAllBytes(videoPath, new byte[] { 0x01 });
			File.WriteAllBytes(micPath, new byte[] { 0x02 });
			File.WriteAllBytes(systemPath, new byte[] { 0x03 });

			var args = FfmpegMediaMuxer.BuildArguments(
				videoPath,
				Path.Combine(tempDir, "out.mp4"),
				micPath,
				systemPath);

			Assert.NotNull(args);
			Assert.Contains("amix=inputs=2", args);
			Assert.Contains("-map \"[aout]\"", args);
		}
		finally
		{
			Directory.Delete(tempDir, recursive: true);
		}
	}

	[Fact]
	public void ClipTimingEstimator_CountsUniqueFrameTimestamps_ForMultiSliceFrames()
	{
		var snapshot = new EncodedPacketSnapshot(new[]
		{
			new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0x01 }, 1000, 1000, true),
			new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x02 }, 1000, 1000, false),
			new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x03 }, 2000, 2000, false),
			new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x04 }, 2000, 2000, false)
		});

		var fps = ClipTimingEstimator.ResolveFps(snapshot, TimeSpan.FromSeconds(1), 60);

		Assert.Equal(2, ClipTimingEstimator.EstimateFrameCount(snapshot.Packets));
		Assert.Equal(2, fps);
	}

	[Fact]
	public void ClipTimingEstimator_UsesObservedDuration_WhenClipWindowIsShorterThanRequested()
	{
		var snapshot = new EncodedPacketSnapshot(new[]
		{
			new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0x01 }, 1000, 1000, true),
			new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x02 }, 1500, 1500, false),
			new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x03 }, 2000, 2000, false)
		});

		var fps = ClipTimingEstimator.ResolveFps(snapshot, TimeSpan.FromSeconds(20), 60);

		Assert.Equal(3, ClipTimingEstimator.EstimateFrameCount(snapshot.Packets));
		Assert.Equal(3, fps);
	}

	[Fact]
	public void RecordingSettingsFactory_UsesSpecificMonitorBounds_WhenConfigured()
	{
		var config = new AppConfig
		{
			NativeNvencEnabled = true,
			Recording = new AppConfig.RecordingConfig
			{
				Fps = 120,
				BitrateMbps = 24,
				ClipSeconds = 30,
				IncludeMicAudio = true,
				IncludeSystemAudio = false,
				CaptureTarget = new AppConfig.CaptureTargetConfig
				{
					Mode = CaptureTargetModes.SpecificMonitor,
					MonitorIndex = 1,
					ExecutablePath = string.Empty
				}
			}
		};

		var settings = RecordingSettingsFactory.Create(
			config,
			new[]
			{
				new Rectangle(0, 0, 1920, 1080),
				new Rectangle(1920, 0, 2560, 1440)
			},
			primaryMonitorIndex: 0,
			getActiveMonitorIndex: static () => 0,
			getActiveWindowHandle: static () => IntPtr.Zero,
			getWindowHandleByExecutablePath: static _ => IntPtr.Zero,
			getWindowBounds: static _ => Rectangle.Empty);

		var target = Assert.IsType<MonitorTarget>(settings.Target);
		Assert.Equal(1, target.MonitorIndex);
		Assert.Equal(2560, settings.Width);
		Assert.Equal(1440, settings.Height);
		Assert.Equal(120, settings.Fps);
		Assert.Equal(24_000_000, settings.Bitrate);
		Assert.Equal(30, settings.ClipSeconds);
		Assert.True(settings.UseNativeNvenc);
	}

	[Fact]
	public void RecordingSettingsFactory_UsesActiveWindowBounds_WhenConfigured()
	{
		var config = new AppConfig
		{
			Recording = new AppConfig.RecordingConfig
			{
				CaptureTarget = new AppConfig.CaptureTargetConfig
				{
					Mode = CaptureTargetModes.ActiveWindow,
					MonitorIndex = 0,
					ExecutablePath = string.Empty
				}
			}
		};

		var hwnd = new IntPtr(42);
		var settings = RecordingSettingsFactory.Create(
			config,
			new[] { new Rectangle(0, 0, 1920, 1080) },
			primaryMonitorIndex: 0,
			getActiveMonitorIndex: static () => 0,
			getActiveWindowHandle: () => hwnd,
			getWindowHandleByExecutablePath: static _ => IntPtr.Zero,
			getWindowBounds: handle => handle == hwnd ? new Rectangle(50, 60, 1600, 900) : Rectangle.Empty);

		var target = Assert.IsType<WindowTarget>(settings.Target);
		Assert.Equal(hwnd, target.Hwnd);
		Assert.Equal(1600, settings.Width);
		Assert.Equal(900, settings.Height);
	}

	[Fact]
	public void RecordingSettingsFactory_Throws_WhenExecutableTargetHasNoRunningWindow()
	{
		var config = new AppConfig
		{
			Recording = new AppConfig.RecordingConfig
			{
				CaptureTarget = new AppConfig.CaptureTargetConfig
				{
					Mode = CaptureTargetModes.ExecutablePath,
					MonitorIndex = 0,
					ExecutablePath = @"C:\Games\Example\game.exe"
				}
			}
		};

		var exception = Assert.Throws<InvalidOperationException>(() => RecordingSettingsFactory.Create(
			config,
			new[] { new Rectangle(0, 0, 1920, 1080) },
			primaryMonitorIndex: 0,
			getActiveMonitorIndex: static () => 0,
			getActiveWindowHandle: static () => IntPtr.Zero,
			getWindowHandleByExecutablePath: static _ => IntPtr.Zero,
			getWindowBounds: static _ => Rectangle.Empty));

		Assert.Contains("No running window matched", exception.Message);
	}

	[Fact]
	public void PreviewFrameSizer_LeavesSmallFramesUnchanged()
	{
		var (width, height) = PreviewFrameSizer.GetScaledSize(800, 450);

		Assert.Equal(800, width);
		Assert.Equal(450, height);
	}

	[Fact]
	public void PreviewFrameSizer_ScalesLandscapeFramesToMaxDimension()
	{
		var (width, height) = PreviewFrameSizer.GetScaledSize(3840, 2160);

		Assert.Equal(960, width);
		Assert.Equal(540, height);
	}

	[Fact]
	public void PreviewFrameSizer_ScalesPortraitFramesToMaxDimension()
	{
		var (width, height) = PreviewFrameSizer.GetScaledSize(1440, 2560);

		Assert.Equal(540, width);
		Assert.Equal(960, height);
	}

	[Fact]
	public void GpuCapabilityProbe_DetectsNvidiaVendorAcrossAnyAdapter()
	{
		var hasNvidia = GpuCapabilityProbe.ContainsVendorId(new[] { 0x8086, 0x10DE, 0x1002 }, 0x10DE);

		Assert.True(hasNvidia);
	}

	[Fact]
	public void GpuCapabilityProbe_ReturnsFalseWhenVendorIsAbsent()
	{
		var hasNvidia = GpuCapabilityProbe.ContainsVendorId(new[] { 0x8086, 0x1002 }, 0x10DE);

		Assert.False(hasNvidia);
	}

	[Fact]
	public void Packetizer_EmitsCompletedNals_AndKeepsTailUntilCompleted()
	{
		var packetizer = new H264AnnexBPacketizer();
		var idr = new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0xAA };
		var nonIdr = new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0xBB };

		var firstChunk = idr.Concat(nonIdr.Take(3)).ToArray();
		var firstPackets = packetizer.Push(firstChunk, pts: 1000, dts: 1000);

		Assert.Empty(firstPackets);

		var secondPackets = packetizer.Push(nonIdr.Skip(3).ToArray(), pts: 1033, dts: 1033);

		Assert.Single(secondPackets);
		Assert.True(secondPackets[0].IsKeyFrame);
		Assert.Equal(idr, secondPackets[0].Data.ToArray());

		var flushed = packetizer.Flush(1033, 1033);
		Assert.Single(flushed);
		Assert.False(flushed[0].IsKeyFrame);
		Assert.Equal(nonIdr, flushed[0].Data.ToArray());
	}

	[Fact]
	public void RingBuffer_SnapshotLast_StartsAtNearestDecodableKeyframeBoundary()
	{
		var ring = new EncodedPacketRingBuffer(TimeSpan.FromSeconds(10));

		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x67, 0x10 }, 1000, 1000, false));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x68, 0x20 }, 1000, 1000, false));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0x30 }, 1000, 1000, true));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x40 }, 1500, 1500, false));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x50 }, 2000, 2000, false));

		var snapshot = ring.SnapshotLast(TimeSpan.FromMilliseconds(700));

		Assert.Equal(5, snapshot.Packets.Count);
		Assert.Equal(7, GetNalType(snapshot.Packets[0].Data.Span));
		Assert.Equal(8, GetNalType(snapshot.Packets[1].Data.Span));
		Assert.True(snapshot.Packets[2].IsKeyFrame);
	}

	[Fact]
	public void RingBuffer_TrimsOldPacketsOutsideRetentionWindow()
	{
		var ring = new EncodedPacketRingBuffer(TimeSpan.FromMilliseconds(500));

		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0x01 }, 1000, 1000, true));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x02 }, 1200, 1200, false));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0x03 }, 2000, 2000, true));

		var snapshot = ring.SnapshotLast(TimeSpan.FromMilliseconds(300));

		Assert.Single(snapshot.Packets);
		Assert.True(snapshot.Packets[0].IsKeyFrame);
		Assert.Equal(0x03, snapshot.Packets[0].Data.Span[^1]);
	}

	[Fact]
	public void RingBuffer_SnapshotWindow_UsesRequestedEndTimestamp()
	{
		var ring = new EncodedPacketRingBuffer(TimeSpan.FromSeconds(10));

		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0x01 }, 1000, 1000, true));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x02 }, 1500, 1500, false));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x03 }, 2000, 2000, false));

		var snapshot = ring.SnapshotWindow(TimeSpan.FromMilliseconds(700), endTimestampMs: 1700);

		Assert.Equal(2, snapshot.Packets.Count);
		Assert.Equal(1000, snapshot.StartTimestampMs);
		Assert.Equal(1500, snapshot.EndTimestampMs);
		Assert.Equal(0x02, snapshot.Packets[^1].Data.Span[^1]);
	}

	[Fact]
	public void AudioRingBuffer_Snapshot_TrimsDataAfterRequestedEndTimestamp()
	{
		var format = new WaveFormat(1000, 16, 1);
		var ring = AudioRingBuffer.Create(format, clipSeconds: 2);

		ring.Write(Enumerable.Repeat((byte)0x11, 200).ToArray(), 0, 200, endTimestampMs: 1000);
		ring.Write(Enumerable.Repeat((byte)0x22, 200).ToArray(), 0, 200, endTimestampMs: 1100);
		ring.Write(Enumerable.Repeat((byte)0x33, 200).ToArray(), 0, 200, endTimestampMs: 1200);

		var snapshot = ring.Snapshot(endTimestampMs: 1100);

		Assert.Equal(400, snapshot.Length);
		Assert.All(snapshot.Take(200), b => Assert.Equal(0x11, b));
		Assert.All(snapshot.Skip(200).Take(200), b => Assert.Equal(0x22, b));
	}

	private static int GetNalType(ReadOnlySpan<byte> data)
	{
		if (data.Length < 5)
		{
			return -1;
		}

		var index = data[2] == 0x01 ? 3 : 4;
		return data[index] & 0x1F;
	}

	private static string CreateTempDirectory()
	{
		var tempDir = Path.Combine(Path.GetTempPath(), $"inplayed-tests-{Guid.NewGuid():N}");
		Directory.CreateDirectory(tempDir);
		return tempDir;
	}
}
