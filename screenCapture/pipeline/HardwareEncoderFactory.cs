public static class HardwareEncoderFactory
{
	public static string GetSelectionDebug()
	{
		var forced = Environment.GetEnvironmentVariable("INPLAYED_ENCODER")?.Trim().ToLowerInvariant();
		var strictGpuOnly = string.Equals(
			Environment.GetEnvironmentVariable("INPLAYED_STRICT_GPU"),
			"1",
			StringComparison.Ordinal);
		var strictNativeNvencOnly = string.Equals(
			Environment.GetEnvironmentVariable("INPLAYED_STRICT_NATIVE_NVENC"),
			"1",
			StringComparison.Ordinal);
		var enableNativeNvenc = string.Equals(
			Environment.GetEnvironmentVariable("INPLAYED_EXPERIMENTAL_NVENC"),
			"1",
			StringComparison.Ordinal);
		var hasNvidiaAdapter = GpuCapabilityProbe.IsNvidiaAdapterPresent();
		var nvencReadiness = enableNativeNvenc ? NvencHardwareEncoder.ProbeReadiness() : new NvencReadiness(false, "disabled", 0, 0, 0, false, false, false, false);
		var hasNvencFfmpeg = FfmpegCapabilities.SupportsEncoder("h264_nvenc");

		return $"forced={forced ?? "auto"};strictGpu={strictGpuOnly};strictNativeNvenc={strictNativeNvencOnly};expNvenc={enableNativeNvenc};nvidia={hasNvidiaAdapter};ffmpegNvenc={hasNvencFfmpeg};nativeNvenc={nvencReadiness.Summary};nativeFnPtrs={nvencReadiness.FunctionPointerCount};nativeRequiredSlots={nvencReadiness.RequiredSlotsPresent};nativeOpenSessionBindable={nvencReadiness.OpenSessionBindable};nativeInitializeEncoderBindable={nvencReadiness.InitializeEncoderBindable};nativePresetApiBindable={nvencReadiness.PresetApiBindable}";
	}

	public static IHardwareEncoder Create(RecordingSettings settings)
	{
		_ = settings;
		var forced = Environment.GetEnvironmentVariable("INPLAYED_ENCODER")?.Trim().ToLowerInvariant();
		var strictGpuOnly = string.Equals(
			Environment.GetEnvironmentVariable("INPLAYED_STRICT_GPU"),
			"1",
			StringComparison.Ordinal);
		var strictNativeNvencOnly = string.Equals(
			Environment.GetEnvironmentVariable("INPLAYED_STRICT_NATIVE_NVENC"),
			"1",
			StringComparison.Ordinal);
		var enableNativeNvenc = string.Equals(
			Environment.GetEnvironmentVariable("INPLAYED_EXPERIMENTAL_NVENC"),
			"1",
			StringComparison.Ordinal);
		var hasNvidiaAdapter = GpuCapabilityProbe.IsNvidiaAdapterPresent();
		var nvencReadiness = enableNativeNvenc ? NvencHardwareEncoder.ProbeReadiness() : new NvencReadiness(false, "disabled", 0, 0, 0, false, false, false, false);

		if (strictNativeNvencOnly)
		{
			if (!hasNvidiaAdapter)
			{
				throw new InvalidOperationException("Strict native NVENC mode enabled, but no NVIDIA adapter was detected.");
			}

			if (!enableNativeNvenc)
			{
				throw new InvalidOperationException("Strict native NVENC mode requires INPLAYED_EXPERIMENTAL_NVENC=1.");
			}

			if (!nvencReadiness.IsReady)
			{
				throw new InvalidOperationException($"Strict native NVENC mode enabled, but readiness check failed: {nvencReadiness.Summary}");
			}

			Console.WriteLine("Encoder forced: native NVENC (strict mode)");
			return new NvencHardwareEncoder();
		}

		if (!string.IsNullOrWhiteSpace(forced))
		{
			Console.WriteLine($"Encoder forced by INPLAYED_ENCODER={forced}");
			switch (forced)
			{
				case "nvenc_native":
					Console.WriteLine($"Native NVENC readiness: {nvencReadiness.Summary}");
					if (!enableNativeNvenc)
					{
						throw new InvalidOperationException("INPLAYED_ENCODER=nvenc_native requires INPLAYED_EXPERIMENTAL_NVENC=1.");
					}

					if (!nvencReadiness.IsReady)
					{
						throw new InvalidOperationException($"INPLAYED_ENCODER=nvenc_native requested, but readiness failed: {nvencReadiness.Summary}");
					}

					return new NvencHardwareEncoder();
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
