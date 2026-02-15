using System.Runtime.InteropServices;

public sealed class NvencHardwareEncoder : IHardwareEncoder
{
	public static NvencReadiness ProbeReadiness()
	{
		if (!GpuCapabilityProbe.IsNvidiaAdapterPresent())
		{
			return new NvencReadiness(false, "nvidia_adapter_not_detected", 0, 0);
		}

		IntPtr nvenc = IntPtr.Zero;
		IntPtr cuda = IntPtr.Zero;
		try
		{
			if (!NativeNvencProbe.TryLoad(out nvenc, out var msg))
			{
				return new NvencReadiness(false, $"probe_failed:{msg}", 0, 0);
			}

			if (!NativeNvencProbe.TryBindGetMaxSupportedVersion(nvenc, out var getMaxVersion, out msg) ||
				getMaxVersion == null)
			{
				return new NvencReadiness(false, $"bind_failed:{msg}", 0, 0);
			}

			var maxRc = getMaxVersion(out var maxVersion);
			if (maxRc != 0)
			{
				return new NvencReadiness(false, $"max_version_query_failed:{NvencNative.ResultToString(maxRc)}", 0, 0);
			}

			if (!NativeNvencProbe.TryLoadCuda(out cuda, out msg))
			{
				return new NvencReadiness(false, $"cuda_missing:{msg}", maxVersion, 0);
			}

			if (!NativeNvencProbe.TryBindCudaInit(cuda, out var cuInit, out msg) || cuInit == null)
			{
				return new NvencReadiness(false, $"cuda_bind_failed:{msg}", maxVersion, 0);
			}

			if (!NativeNvencProbe.TryBindCudaDriverGetVersion(cuda, out var cuGetVersion, out msg) || cuGetVersion == null)
			{
				return new NvencReadiness(false, $"cuda_bind_failed:{msg}", maxVersion, 0);
			}

			var cuInitRc = cuInit(0);
			if (cuInitRc != 0)
			{
				return new NvencReadiness(false, $"cuda_init_failed:{NvencNative.CudaResultToString(cuInitRc)}", maxVersion, 0);
			}

			var cuVersionRc = cuGetVersion(out var cudaVersion);
			if (cuVersionRc != 0)
			{
				return new NvencReadiness(false, $"cuda_version_failed:{NvencNative.CudaResultToString(cuVersionRc)}", maxVersion, 0);
			}

			return new NvencReadiness(true, "ready", maxVersion, cudaVersion);
		}
		finally
		{
			if (nvenc != IntPtr.Zero)
			{
				try { NativeLibrary.Free(nvenc); } catch { }
			}
			if (cuda != IntPtr.Zero)
			{
				try { NativeLibrary.Free(cuda); } catch { }
			}
		}
	}

	public string BackendName => "NvencNative";
	private string _status = "uninitialized";
	private IntPtr _nvencLib;
	private IntPtr _cudaLib;
	private NvencNative.NvEncodeApiCreateInstanceDelegate? _createInstance;
	private NvencNative.NvEncodeApiGetMaxSupportedVersionDelegate? _getMaxSupportedVersion;
	private NvencNative.CuInitDelegate? _cuInit;
	private NvencNative.CuDriverGetVersionDelegate? _cuDriverGetVersion;
	private uint _maxSupportedVersion;
	private int _cudaDriverVersion;
	private IntPtr _functionListBuffer;
	private uint _functionListVersion;
	private int _createInstanceRc;
	private int _functionPointerCount;

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

		if (!NativeNvencProbe.TryBindCudaInit(_cudaLib, out _cuInit, out message))
		{
			_status = $"cuda_bind_failed:{message}";
			throw new NotSupportedException($"CUDA bind failed: {message}");
		}

		if (!NativeNvencProbe.TryBindCudaDriverGetVersion(_cudaLib, out _cuDriverGetVersion, out message))
		{
			_status = $"cuda_bind_failed:{message}";
			throw new NotSupportedException($"CUDA bind failed: {message}");
		}

		var cuInit = _cuInit;
		var cuGetVersion = _cuDriverGetVersion;
		if (cuInit == null || cuGetVersion == null)
		{
			_status = "cuda_delegate_null";
			throw new NotSupportedException("CUDA delegates are null.");
		}

		var cuInitRc = cuInit(0);
		if (cuInitRc != 0)
		{
			var cudaRc = NvencNative.CudaResultToString(cuInitRc);
			_status = $"cuda_init_failed:{cudaRc}";
			throw new NotSupportedException($"CUDA initialization failed: {cudaRc}");
		}

		var cuVersionRc = cuGetVersion(out _cudaDriverVersion);
		if (cuVersionRc != 0)
		{
			var cudaRc = NvencNative.CudaResultToString(cuVersionRc);
			_status = $"cuda_version_failed:{cudaRc}";
			throw new NotSupportedException($"CUDA driver version query failed: {cudaRc}");
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
			var rcName = NvencNative.ResultToString(rc);
			_status = $"max_version_query_failed:{rcName}";
			throw new NotSupportedException($"NVENC max supported version query failed: {rcName}");
		}

		_functionListBuffer = NvencFunctionList.Allocate(_maxSupportedVersion, out _functionListVersion);
		var createInstance = _createInstance;
		if (createInstance == null)
		{
			_status = "create_instance_delegate_null";
			throw new NotSupportedException("NVENC create-instance delegate is null.");
		}

		_createInstanceRc = createInstance(_functionListBuffer);
		if (_createInstanceRc != 0)
		{
			var rcName = NvencNative.ResultToString(_createInstanceRc);
			_status = $"create_instance_failed:{rcName}";
			throw new NotSupportedException($"NvEncodeAPICreateInstance failed: {rcName}");
		}

		_functionPointerCount = NvencFunctionList.CountNonZeroPointerSlots(_functionListBuffer, 96);

		_status = $"create_instance_ok_fnptrs={_functionPointerCount}_cuda={FormatCudaDriverVersion(_cudaDriverVersion)}_maxver=0x{_maxSupportedVersion:X8}({FormatVersionWords(_maxSupportedVersion)})_fnlist=0x{_functionListVersion:X8}_but_not_implemented";
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
		return $"{_status};maxVersion=0x{_maxSupportedVersion:X8}({FormatVersionWords(_maxSupportedVersion)});cudaDriver={FormatCudaDriverVersion(_cudaDriverVersion)};fnListVersion=0x{_functionListVersion:X8};createInstanceRc={NvencNative.ResultToString(_createInstanceRc)};fnPtrCount={_functionPointerCount}";
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
		_cuInit = null;
		_cuDriverGetVersion = null;
		NvencFunctionList.Free(ref _functionListBuffer);
		_functionListVersion = 0;
		_functionPointerCount = 0;
		_maxSupportedVersion = 0;
		_cudaDriverVersion = 0;
		_createInstanceRc = 0;
	}

	private static string FormatVersionWords(uint version)
	{
		var hi = (version >> 16) & 0xFFFF;
		var lo = version & 0xFFFF;
		return $"{hi}.{lo}";
	}

	private static string FormatCudaDriverVersion(int version)
	{
		if (version <= 0)
		{
			return "unknown";
		}

		var major = version / 1000;
		var minor = (version % 1000) / 10;
		return $"{major}.{minor}";
	}
}
