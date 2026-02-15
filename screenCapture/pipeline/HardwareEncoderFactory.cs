public static class HardwareEncoderFactory
{
	public static string GetSelectionDebug()
	{
		var forced = Environment.GetEnvironmentVariable("INPLAYED_ENCODER")?.Trim().ToLowerInvariant();
		var strictGpuOnly = string.Equals(
			Environment.GetEnvironmentVariable("INPLAYED_STRICT_GPU"),
			"1",
			StringComparison.Ordinal);
		var enableNativeNvenc = string.Equals(
			Environment.GetEnvironmentVariable("INPLAYED_EXPERIMENTAL_NVENC"),
			"1",
			StringComparison.Ordinal);
		var hasNvidiaAdapter = GpuCapabilityProbe.IsNvidiaAdapterPresent();
		var nvencReadiness = enableNativeNvenc ? NvencHardwareEncoder.ProbeReadiness() : new NvencReadiness(false, "disabled", 0, 0, 0);
		var hasNvencFfmpeg = FfmpegCapabilities.SupportsEncoder("h264_nvenc");

		return $"forced={forced ?? "auto"};strictGpu={strictGpuOnly};expNvenc={enableNativeNvenc};nvidia={hasNvidiaAdapter};ffmpegNvenc={hasNvencFfmpeg};nativeNvenc={nvencReadiness.Summary};nativeFnPtrs={nvencReadiness.FunctionPointerCount}";
	}

	public static IHardwareEncoder Create(RecordingSettings settings)
	{
		_ = settings;
		var forced = Environment.GetEnvironmentVariable("INPLAYED_ENCODER")?.Trim().ToLowerInvariant();
		var strictGpuOnly = string.Equals(
			Environment.GetEnvironmentVariable("INPLAYED_STRICT_GPU"),
			"1",
			StringComparison.Ordinal);
		var enableNativeNvenc = string.Equals(
			Environment.GetEnvironmentVariable("INPLAYED_EXPERIMENTAL_NVENC"),
			"1",
			StringComparison.Ordinal);
		var hasNvidiaAdapter = GpuCapabilityProbe.IsNvidiaAdapterPresent();
		var nvencReadiness = enableNativeNvenc ? NvencHardwareEncoder.ProbeReadiness() : new NvencReadiness(false, "disabled", 0, 0, 0);

		if (!string.IsNullOrWhiteSpace(forced))
		{
			Console.WriteLine($"Encoder forced by INPLAYED_ENCODER={forced}");
			switch (forced)
			{
				case "nvenc_native":
					Console.WriteLine($"Native NVENC readiness: {nvencReadiness.Summary}");
					return new AdaptiveHardwareEncoder(
						() => new NvencHardwareEncoder(),
						() => new FfmpegPacketRingHardwareEncoder("h264_nvenc"),
						() => new FfmpegPacketRingHardwareEncoder("libx264"),
						() => new CpuReadbackHardwareEncoder("libx264"));
				case "nvenc_packet":
					return new AdaptiveHardwareEncoder(
						() => new FfmpegPacketRingHardwareEncoder("h264_nvenc"),
						() => new FfmpegPacketRingHardwareEncoder("libx264"),
						() => new CpuReadbackHardwareEncoder("libx264"));
				case "x264_packet":
					return new AdaptiveHardwareEncoder(
						() => new FfmpegPacketRingHardwareEncoder("libx264"),
						() => new CpuReadbackHardwareEncoder("libx264"));
				case "cpu":
					return new CpuReadbackHardwareEncoder("libx264");
				default:
					Console.WriteLine("Unknown INPLAYED_ENCODER value. Falling back to auto selection.");
					break;
			}
		}

		if (hasNvidiaAdapter && FfmpegCapabilities.SupportsEncoder("h264_nvenc"))
		{
			Console.WriteLine("Encoder preferred: h264_nvenc (ffmpeg packet ring)");
			if (enableNativeNvenc)
			{
				Console.WriteLine("Native NVENC experimental path enabled");
				if (nvencReadiness.IsReady)
				{
					return new AdaptiveHardwareEncoder(
						() => new NvencHardwareEncoder(),
						() => new FfmpegPacketRingHardwareEncoder("h264_nvenc"),
						() => new FfmpegPacketRingHardwareEncoder("libx264"));
				}

				Console.WriteLine($"Native NVENC skipped: {nvencReadiness.Summary}");
				return new AdaptiveHardwareEncoder(
					() => new FfmpegPacketRingHardwareEncoder("h264_nvenc"),
					() => new FfmpegPacketRingHardwareEncoder("libx264"));
			}

			if (strictGpuOnly)
			{
				Console.WriteLine("Strict GPU mode enabled; CPU fallback disabled");
				return new AdaptiveHardwareEncoder(
					() => new FfmpegPacketRingHardwareEncoder("h264_nvenc"),
					() => new FfmpegPacketRingHardwareEncoder("libx264"));
			}

			return new AdaptiveHardwareEncoder(
				() => new FfmpegPacketRingHardwareEncoder("h264_nvenc"),
				() => new FfmpegPacketRingHardwareEncoder("libx264"),
				() => new CpuReadbackHardwareEncoder("libx264"));
		}
		else if (!hasNvidiaAdapter)
		{
			Console.WriteLine("NVIDIA adapter not detected; skipping NVENC backends");
		}

		Console.WriteLine("Encoder preferred: libx264 (ffmpeg packet ring)");
		if (strictGpuOnly)
		{
			Console.WriteLine("Strict GPU mode enabled; CPU fallback disabled");
			return new AdaptiveHardwareEncoder(
				() => new FfmpegPacketRingHardwareEncoder("libx264"));
		}

		return new AdaptiveHardwareEncoder(
			() => new FfmpegPacketRingHardwareEncoder("libx264"),
			() => new CpuReadbackHardwareEncoder("libx264"));
	}
}
