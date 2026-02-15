using System.Runtime.InteropServices;

public sealed class NvencHardwareEncoder : IHardwareEncoder
{
	public string BackendName => "NvencNative";
	private string _status = "uninitialized";
	private IntPtr _nvencLib;
	private IntPtr _cudaLib;
	private NvencNative.NvEncodeApiCreateInstanceDelegate? _createInstance;
	private NvencNative.NvEncodeApiGetMaxSupportedVersionDelegate? _getMaxSupportedVersion;
	private uint _maxSupportedVersion;

	public void Start(RecordingSettings settings)
	{
		_ = settings;
		if (!GpuCapabilityProbe.IsNvidiaAdapterPresent())
		{
			_status = "nvidia_adapter_not_detected";
			throw new NotSupportedException("NVENC requires an NVIDIA adapter.");
		}

		if (!NativeNvencProbe.TryLoad(out _nvencLib, out var message))
		{
			_status = $"probe_failed:{message}";
			throw new NotSupportedException($"NVENC probe failed: {message}");
		}

		if (!NativeNvencProbe.HasCreateInstanceExport(_nvencLib, out message))
		{
			_status = $"invalid_runtime:{message}";
			throw new NotSupportedException($"NVENC runtime missing required export: {message}");
		}

		if (!NativeNvencProbe.TryBindCreateInstance(_nvencLib, out _createInstance, out message))
		{
			_status = $"bind_failed:{message}";
			throw new NotSupportedException($"NVENC runtime export bind failed: {message}");
		}

		if (!NativeNvencProbe.TryBindGetMaxSupportedVersion(_nvencLib, out _getMaxSupportedVersion, out message))
		{
			_status = $"bind_failed:{message}";
			throw new NotSupportedException($"NVENC max-version export bind failed: {message}");
		}

		if (!NativeNvencProbe.TryLoadCuda(out _cudaLib, out message))
		{
			_status = $"cuda_missing:{message}";
			throw new NotSupportedException($"CUDA runtime probe failed: {message}");
		}

		var getMaxVersion = _getMaxSupportedVersion;
		if (getMaxVersion == null)
		{
			_status = "max_version_delegate_null";
			throw new NotSupportedException("NVENC max supported version delegate is null.");
		}

		var rc = getMaxVersion(out _maxSupportedVersion);
		if (rc != 0)
		{
			_status = $"max_version_query_failed:0x{rc:X8}";
			throw new NotSupportedException($"NVENC max supported version query failed: 0x{rc:X8}");
		}

		_status = $"runtime_bound_cuda_loaded_maxver=0x{_maxSupportedVersion:X8}({FormatVersionWords(_maxSupportedVersion)})_but_not_implemented";
		throw new NotImplementedException(
			"NVENC runtime detected, but native session creation/encode path is not implemented yet.");
	}

	public void Encode(TextureFrameRef frame)
	{
		_ = frame;
	}

	public void Reconfigure(int width, int height)
	{
		_ = width;
		_ = height;
	}

	public Task FlushRecentAsync(string outputPath, TimeSpan clipLength, CancellationToken token = default)
	{
		_ = outputPath;
		_ = clipLength;
		_ = token;
		return Task.CompletedTask;
	}

	public string GetDebugStatus()
	{
		return $"{_status};maxVersion=0x{_maxSupportedVersion:X8}({FormatVersionWords(_maxSupportedVersion)})";
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
		if (_nvencLib != IntPtr.Zero)
		{
			try
			{
				NativeLibrary.Free(_nvencLib);
			}
			catch
			{
			}

			_nvencLib = IntPtr.Zero;
		}

		if (_cudaLib != IntPtr.Zero)
		{
			try
			{
				NativeLibrary.Free(_cudaLib);
			}
			catch
			{
			}

			_cudaLib = IntPtr.Zero;
		}

		_createInstance = null;
		_getMaxSupportedVersion = null;
		_maxSupportedVersion = 0;
	}

	private static string FormatVersionWords(uint version)
	{
		var hi = (version >> 16) & 0xFFFF;
		var lo = version & 0xFFFF;
		return $"{hi}.{lo}";
	}
}
