using System.Runtime.InteropServices;

public sealed class NvencHardwareEncoder : IHardwareEncoder
{
	public static NvencReadiness ProbeReadiness()
	{
		if (!GpuCapabilityProbe.IsNvidiaAdapterPresent())
		{
			return new NvencReadiness(false, "nvidia_adapter_not_detected", 0, 0, 0, false, false, false);
		}

		IntPtr nvenc = IntPtr.Zero;
		IntPtr cuda = IntPtr.Zero;
		IntPtr fnList = IntPtr.Zero;
		try
		{
			if (!NativeNvencProbe.TryLoad(out nvenc, out var msg))
			{
				return new NvencReadiness(false, $"probe_failed:{msg}", 0, 0, 0, false, false, false);
			}

			if (!NativeNvencProbe.TryBindCreateInstance(nvenc, out var createInstance, out msg) ||
				createInstance == null)
			{
				return new NvencReadiness(false, $"bind_failed:{msg}", 0, 0, 0, false, false, false);
			}

			if (!NativeNvencProbe.TryBindGetMaxSupportedVersion(nvenc, out var getMaxVersion, out msg) ||
				getMaxVersion == null)
			{
				return new NvencReadiness(false, $"bind_failed:{msg}", 0, 0, 0, false, false, false);
			}

			var maxRc = getMaxVersion(out var maxVersion);
			if (maxRc != 0)
			{
				return new NvencReadiness(false, $"max_version_query_failed:{NvencNative.ResultToString(maxRc)}", 0, 0, 0, false, false, false);
			}

			if (!NvencNative.IsApiCompatible(maxVersion, NvencNative.NVENCAPI_VERSION))
			{
				return new NvencReadiness(false, $"api_version_too_old:max=0x{maxVersion:X8};required=0x{NvencNative.NVENCAPI_VERSION:X8}", maxVersion, 0, 0, false, false, false);
			}

			fnList = NvencFunctionList.Allocate(maxVersion, out _);
			var ciRc = createInstance(fnList);
			if (ciRc != 0)
			{
				return new NvencReadiness(false, $"create_instance_failed:{NvencNative.ResultToString(ciRc)}", maxVersion, 0, 0, false, false, false);
			}

			var fnPtrCount = NvencFunctionList.CountNonZeroPointerSlots(fnList, 96);
			if (fnPtrCount == 0)
			{
				return new NvencReadiness(false, "create_instance_empty_function_list", maxVersion, 0, 0, false, false, false);
			}

			var requiredSlots = NvencFunctionListInspector.ReadRequiredSlots(fnList);
			if (!requiredSlots.AllPresent)
			{
				return new NvencReadiness(false, "required_function_slots_missing", maxVersion, 0, fnPtrCount, false, false, false);
			}

			var openSessionPtr = NvencFunctionListInspector.ReadPointerAtSlot(fnList, 0);
			if (!NvencApiBootstrap.TryBindOpenSessionDelegate(openSessionPtr, out var openSessionDelegate, out msg) ||
				openSessionDelegate == null)
			{
				return new NvencReadiness(false, $"open_session_bind_failed:{msg}", maxVersion, 0, fnPtrCount, true, false, false);
			}

			var initializePtr = NvencFunctionListInspector.ReadPointerAtSlot(fnList, 12);
			if (!NvencApiBootstrap.TryBindInitializeEncoderDelegate(initializePtr, out var initializeDelegate, out msg) ||
				initializeDelegate == null)
			{
				return new NvencReadiness(false, $"initialize_encoder_bind_failed:{msg}", maxVersion, 0, fnPtrCount, true, true, false);
			}

			if (!NativeNvencProbe.TryLoadCuda(out cuda, out msg))
			{
				return new NvencReadiness(false, $"cuda_missing:{msg}", maxVersion, 0, fnPtrCount, true, true, true);
			}

			if (!NativeNvencProbe.TryBindCudaInit(cuda, out var cuInit, out msg) || cuInit == null)
			{
				return new NvencReadiness(false, $"cuda_bind_failed:{msg}", maxVersion, 0, fnPtrCount, true, true, true);
			}

			if (!NativeNvencProbe.TryBindCudaDriverGetVersion(cuda, out var cuGetVersion, out msg) || cuGetVersion == null)
			{
				return new NvencReadiness(false, $"cuda_bind_failed:{msg}", maxVersion, 0, fnPtrCount, true, true, true);
			}

			var cuInitRc = cuInit(0);
			if (cuInitRc != 0)
			{
				return new NvencReadiness(false, $"cuda_init_failed:{NvencNative.CudaResultToString(cuInitRc)}", maxVersion, 0, fnPtrCount, true, true, true);
			}

			var cuVersionRc = cuGetVersion(out var cudaVersion);
			if (cuVersionRc != 0)
			{
				return new NvencReadiness(false, $"cuda_version_failed:{NvencNative.CudaResultToString(cuVersionRc)}", maxVersion, 0, fnPtrCount, true, true, true);
			}

			return new NvencReadiness(true, "ready", maxVersion, cudaVersion, fnPtrCount, true, true, true);
		}
		finally
		{
			NvencFunctionList.Free(ref fnList);
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
	private IntPtr _openSessionPtr;
	private NvencNative.NvEncOpenEncodeSessionExDelegate? _openSession;
	private int _openSessionRc;
	private IntPtr _encoderSession;
	private D3D11DeviceBundle? _bootstrapDevice;
	private IntPtr _getEncodeGuidCountPtr;
	private IntPtr _getEncodeProfileGuidCountPtr;
	private IntPtr _getEncodeGuidsPtr;
	private IntPtr _getEncodeProfileGuidsPtr;
	private IntPtr _getEncodePresetCountPtr;
	private IntPtr _getEncodePresetGuidsPtr;
	private IntPtr _initializeEncoderPtr;
	private NvencNative.NvEncGetEncodeGuidCountDelegate? _getEncodeGuidCount;
	private NvencNative.NvEncGetEncodeGUIDsDelegate? _getEncodeGuids;
	private NvencNative.NvEncGetEncodeProfileGuidCountDelegate? _getEncodeProfileGuidCount;
	private NvencNative.NvEncGetEncodeProfileGUIDsDelegate? _getEncodeProfileGuids;
	private NvencNative.NvEncGetEncodePresetCountDelegate? _getEncodePresetCount;
	private NvencNative.NvEncGetEncodePresetGUIDsDelegate? _getEncodePresetGuids;
	private NvencNative.NvEncInitializeEncoderDelegate? _initializeEncoder;
	private int _getEncodeGuidCountRc;
	private uint _encodeGuidCount;
	private int _getEncodeGuidsRc;
	private int _getEncodeProfileGuidCountH264Rc;
	private int _getEncodeProfileGuidCountHevcRc;
	private uint _h264ProfileGuidCount;
	private uint _hevcProfileGuidCount;
	private int _getEncodePresetCountRc;
	private int _getEncodePresetGuidsRc;
	private uint _presetGuidCount;
	private bool _supportsH264;
	private bool _supportsHevc;
	private Guid _selectedCodecGuid;
	private string _selectedCodecName = "unknown";
	private Guid _selectedPresetGuid;

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

		if (!NvencNative.IsApiCompatible(_maxSupportedVersion, NvencNative.NVENCAPI_VERSION))
		{
			_status = $"api_version_too_old:max=0x{_maxSupportedVersion:X8};required=0x{NvencNative.NVENCAPI_VERSION:X8}";
			throw new NotSupportedException(
				$"NVENC runtime is too old. Max=0x{_maxSupportedVersion:X8}, required=0x{NvencNative.NVENCAPI_VERSION:X8}.");
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
		if (!NvencApiBootstrap.TryReadOpenSessionPointer(_functionListBuffer, out _openSessionPtr, out var openPtrMsg))
		{
			_status = $"create_instance_ok_but_open_session_missing:{openPtrMsg}";
			throw new NotSupportedException($"NVENC bootstrap failed: {openPtrMsg}");
		}

		_getEncodeGuidCountPtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 1);
		_getEncodeProfileGuidCountPtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 2);
		_getEncodeGuidsPtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 3);
		_getEncodeProfileGuidsPtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 4);
		_getEncodePresetCountPtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 8);
		_getEncodePresetGuidsPtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 9);
		_initializeEncoderPtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 12);

		if (!NvencApiBootstrap.TryBindOpenSessionDelegate(_openSessionPtr, out _openSession, out openPtrMsg))
		{
			_status = $"open_session_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC open-session delegate bind failed: {openPtrMsg}");
		}

		if (!NvencApiBootstrap.TryBindGetEncodeGuidCountDelegate(
				_getEncodeGuidCountPtr,
				out _getEncodeGuidCount,
				out openPtrMsg))
		{
			_status = $"get_encode_guid_count_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC get-encode-guid-count bind failed: {openPtrMsg}");
		}

		if (!NvencApiBootstrap.TryBindGetEncodeProfileGuidCountDelegate(
				_getEncodeProfileGuidCountPtr,
				out _getEncodeProfileGuidCount,
				out openPtrMsg))
		{
			_status = $"get_encode_profile_guid_count_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC get-encode-profile-guid-count bind failed: {openPtrMsg}");
		}

		if (!NvencApiBootstrap.TryBindGetEncodeGuidsDelegate(
				_getEncodeGuidsPtr,
				out _getEncodeGuids,
				out openPtrMsg))
		{
			_status = $"get_encode_guids_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC get-encode-guids bind failed: {openPtrMsg}");
		}

		_ = NvencApiBootstrap.TryBindGetEncodeProfileGuidsDelegate(
			_getEncodeProfileGuidsPtr,
			out _getEncodeProfileGuids,
			out _);
		_ = NvencApiBootstrap.TryBindGetEncodePresetCountDelegate(
			_getEncodePresetCountPtr,
			out _getEncodePresetCount,
			out _);
		_ = NvencApiBootstrap.TryBindGetEncodePresetGuidsDelegate(
			_getEncodePresetGuidsPtr,
			out _getEncodePresetGuids,
			out _);

		if (!NvencApiBootstrap.TryBindInitializeEncoderDelegate(
			_initializeEncoderPtr,
			out _initializeEncoder,
			out openPtrMsg))
		{
			_status = $"initialize_encoder_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC initialize-encoder bind failed: {openPtrMsg}");
		}

		var openSession = _openSession;
		if (openSession == null)
		{
			_status = "open_session_delegate_null";
			throw new NotSupportedException("NVENC open-session delegate is null.");
		}

		_bootstrapDevice = D3D11Helper.CreateDevice();
		var openParams = new NvencNative.NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS
		{
			version = NvencNative.EncodeStructVersion<NvencNative.NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS>(1),
			deviceType = NvencNative.NV_ENC_DEVICE_TYPE_DIRECTX,
			device = _bootstrapDevice.Device.NativePointer,
			reserved = IntPtr.Zero,
			apiVersion = NvencNative.NVENCAPI_VERSION
		};

		var openParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NvencNative.NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS>());
		try
		{
			Marshal.StructureToPtr(openParams, openParamsPtr, false);
			_openSessionRc = openSession(openParamsPtr, out _encoderSession);
		}
		finally
		{
			Marshal.FreeHGlobal(openParamsPtr);
		}

		if (_openSessionRc != 0)
		{
			var rcName = NvencNative.ResultToString(_openSessionRc);
			_status = $"open_session_failed:{rcName}";
			throw new NotSupportedException($"NVENC open session failed: {rcName}");
		}

		var getCount = _getEncodeGuidCount;
		var getGuids = _getEncodeGuids;
		var getProfileCount = _getEncodeProfileGuidCount;
		if (getCount == null || getGuids == null || getProfileCount == null)
		{
			_status = "codec_query_delegate_null";
			throw new NotSupportedException("NVENC codec query delegates are null.");
		}

		_getEncodeGuidCountRc = getCount(_encoderSession, out _encodeGuidCount);
		if (_getEncodeGuidCountRc != 0)
		{
			var rcName = NvencNative.ResultToString(_getEncodeGuidCountRc);
			_status = $"get_encode_guid_count_failed:{rcName}";
			throw new NotSupportedException($"NVENC get encode guid count failed: {rcName}");
		}

		if (_encodeGuidCount == 0)
		{
			_status = "get_encode_guid_count_zero";
			throw new NotSupportedException("NVENC reported zero supported encode GUIDs.");
		}

		var guidSize = Marshal.SizeOf<Guid>();
		var guidBuffer = Marshal.AllocHGlobal((int)(_encodeGuidCount * (uint)guidSize));
		try
		{
			_getEncodeGuidsRc = getGuids(_encoderSession, guidBuffer, _encodeGuidCount, out var copied);
			if (_getEncodeGuidsRc != 0)
			{
				var rcName = NvencNative.ResultToString(_getEncodeGuidsRc);
				_status = $"get_encode_guids_failed:{rcName}";
				throw new NotSupportedException($"NVENC get encode GUIDs failed: {rcName}");
			}

			var count = Math.Min(_encodeGuidCount, copied);
			for (var i = 0; i < count; i++)
			{
				var ptr = IntPtr.Add(guidBuffer, i * guidSize);
				var guid = Marshal.PtrToStructure<Guid>(ptr);
				if (guid == NvencNative.NV_ENC_CODEC_H264_GUID)
				{
					_supportsH264 = true;
				}
				else if (guid == NvencNative.NV_ENC_CODEC_HEVC_GUID)
				{
					_supportsHevc = true;
				}
			}
		}
		finally
		{
			Marshal.FreeHGlobal(guidBuffer);
		}

		if (_supportsH264)
		{
			_getEncodeProfileGuidCountH264Rc = QueryProfileGuidCount(
				_encoderSession,
				getProfileCount,
				NvencNative.NV_ENC_CODEC_H264_GUID,
				out _h264ProfileGuidCount);
		}

		if (_supportsHevc)
		{
			_getEncodeProfileGuidCountHevcRc = QueryProfileGuidCount(
				_encoderSession,
				getProfileCount,
				NvencNative.NV_ENC_CODEC_HEVC_GUID,
				out _hevcProfileGuidCount);
		}

		if (_supportsH264)
		{
			_selectedCodecGuid = NvencNative.NV_ENC_CODEC_H264_GUID;
			_selectedCodecName = "h264";
		}
		else if (_supportsHevc)
		{
			_selectedCodecGuid = NvencNative.NV_ENC_CODEC_HEVC_GUID;
			_selectedCodecName = "hevc";
		}
		else
		{
			_selectedCodecGuid = Guid.Empty;
			_selectedCodecName = "none";
		}

		if (_selectedCodecGuid == Guid.Empty)
		{
			_status = "no_supported_codec_guid";
			throw new NotSupportedException("NVENC reported no supported H.264/HEVC codec GUID.");
		}

		var getPresetCount = _getEncodePresetCount;
		var getPresetGuids = _getEncodePresetGuids;
		if (getPresetCount != null && getPresetGuids != null)
		{
			_getEncodePresetCountRc = QueryPresetGuidCount(
				_encoderSession,
				getPresetCount,
				_selectedCodecGuid,
				out _presetGuidCount);

			if (_getEncodePresetCountRc == 0 && _presetGuidCount > 0)
			{
				_getEncodePresetGuidsRc = QueryPresetGuids(
					_encoderSession,
					getPresetGuids,
					_selectedCodecGuid,
					_presetGuidCount,
					out _selectedPresetGuid);
			}
		}

		_status = $"open_session_ok_fnptrs={_functionPointerCount}_cuda={FormatCudaDriverVersion(_cudaDriverVersion)}_maxver=0x{_maxSupportedVersion:X8}({FormatVersionWords(_maxSupportedVersion)})_fnlist=0x{_functionListVersion:X8}_session=0x{_encoderSession.ToInt64():X}_codecCount={_encodeGuidCount}_h264={_supportsH264}_hevc={_supportsHevc}_h264Profiles={_h264ProfileGuidCount}_hevcProfiles={_hevcProfileGuidCount}_selectedCodec={_selectedCodecName}_presetCount={_presetGuidCount}_selectedPresetGuid={_selectedPresetGuid}_initBound={(_initializeEncoder != null ? 1 : 0)}_but_not_implemented";
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
		return $"{_status};maxVersion=0x{_maxSupportedVersion:X8}({FormatVersionWords(_maxSupportedVersion)});cudaDriver={FormatCudaDriverVersion(_cudaDriverVersion)};fnListVersion=0x{_functionListVersion:X8};createInstanceRc={NvencNative.ResultToString(_createInstanceRc)};fnPtrCount={_functionPointerCount};openSessionPtr=0x{_openSessionPtr.ToInt64():X};openSessionBound={(_openSession != null ? 1 : 0)};openSessionRc={NvencNative.ResultToString(_openSessionRc)};encoderSession=0x{_encoderSession.ToInt64():X};getEncodeGuidCountPtr=0x{_getEncodeGuidCountPtr.ToInt64():X};getEncodeGuidCountBound={(_getEncodeGuidCount != null ? 1 : 0)};getEncodeGuidCountRc={NvencNative.ResultToString(_getEncodeGuidCountRc)};getEncodeGuidsPtr=0x{_getEncodeGuidsPtr.ToInt64():X};getEncodeGuidsBound={(_getEncodeGuids != null ? 1 : 0)};getEncodeGuidsRc={NvencNative.ResultToString(_getEncodeGuidsRc)};codecCount={_encodeGuidCount};supportsH264={_supportsH264};supportsHevc={_supportsHevc};selectedCodec={_selectedCodecName};selectedCodecGuid={_selectedCodecGuid};getEncodeProfileGuidCountPtr=0x{_getEncodeProfileGuidCountPtr.ToInt64():X};getEncodeProfileGuidCountBound={(_getEncodeProfileGuidCount != null ? 1 : 0)};getEncodeProfileGuidsPtr=0x{_getEncodeProfileGuidsPtr.ToInt64():X};getEncodeProfileGuidsBound={(_getEncodeProfileGuids != null ? 1 : 0)};getEncodePresetCountPtr=0x{_getEncodePresetCountPtr.ToInt64():X};getEncodePresetCountBound={(_getEncodePresetCount != null ? 1 : 0)};getEncodePresetCountRc={NvencNative.ResultToString(_getEncodePresetCountRc)};getEncodePresetGuidsPtr=0x{_getEncodePresetGuidsPtr.ToInt64():X};getEncodePresetGuidsBound={(_getEncodePresetGuids != null ? 1 : 0)};getEncodePresetGuidsRc={NvencNative.ResultToString(_getEncodePresetGuidsRc)};presetCount={_presetGuidCount};selectedPresetGuid={_selectedPresetGuid};initializeEncoderPtr=0x{_initializeEncoderPtr.ToInt64():X};initializeEncoderBound={(_initializeEncoder != null ? 1 : 0)};h264ProfileCount={_h264ProfileGuidCount};h264ProfileCountRc={NvencNative.ResultToString(_getEncodeProfileGuidCountH264Rc)};hevcProfileCount={_hevcProfileGuidCount};hevcProfileCountRc={NvencNative.ResultToString(_getEncodeProfileGuidCountHevcRc)}";
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
		_openSessionPtr = IntPtr.Zero;
		_openSession = null;
		_openSessionRc = 0;
		_encoderSession = IntPtr.Zero;
		_getEncodeGuidCountPtr = IntPtr.Zero;
		_getEncodeProfileGuidCountPtr = IntPtr.Zero;
		_getEncodeGuidsPtr = IntPtr.Zero;
		_getEncodeProfileGuidsPtr = IntPtr.Zero;
		_getEncodePresetCountPtr = IntPtr.Zero;
		_getEncodePresetGuidsPtr = IntPtr.Zero;
		_initializeEncoderPtr = IntPtr.Zero;
		_getEncodeGuidCount = null;
		_getEncodeGuids = null;
		_getEncodeProfileGuidCount = null;
		_getEncodeProfileGuids = null;
		_getEncodePresetCount = null;
		_getEncodePresetGuids = null;
		_initializeEncoder = null;
		_getEncodeGuidCountRc = 0;
		_getEncodeGuidsRc = 0;
		_encodeGuidCount = 0;
		_getEncodeProfileGuidCountH264Rc = 0;
		_getEncodeProfileGuidCountHevcRc = 0;
		_getEncodePresetCountRc = 0;
		_getEncodePresetGuidsRc = 0;
		_h264ProfileGuidCount = 0;
		_hevcProfileGuidCount = 0;
		_presetGuidCount = 0;
		_supportsH264 = false;
		_supportsHevc = false;
		_selectedCodecGuid = Guid.Empty;
		_selectedCodecName = "unknown";
		_selectedPresetGuid = Guid.Empty;
		_maxSupportedVersion = 0;
		_cudaDriverVersion = 0;
		_createInstanceRc = 0;
		_bootstrapDevice?.Dispose();
		_bootstrapDevice = null;
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

	private static int QueryProfileGuidCount(
		IntPtr encoderSession,
		NvencNative.NvEncGetEncodeProfileGuidCountDelegate getProfileCount,
		Guid codecGuid,
		out uint profileCount)
	{
		profileCount = 0;
		var codecPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
		try
		{
			Marshal.StructureToPtr(codecGuid, codecPtr, false);
			return getProfileCount(encoderSession, codecPtr, out profileCount);
		}
		finally
		{
			Marshal.FreeHGlobal(codecPtr);
		}
	}

	private static int QueryPresetGuidCount(
		IntPtr encoderSession,
		NvencNative.NvEncGetEncodePresetCountDelegate getPresetCount,
		Guid codecGuid,
		out uint presetCount)
	{
		presetCount = 0;
		var codecPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
		try
		{
			Marshal.StructureToPtr(codecGuid, codecPtr, false);
			return getPresetCount(encoderSession, codecPtr, out presetCount);
		}
		finally
		{
			Marshal.FreeHGlobal(codecPtr);
		}
	}

	private static int QueryPresetGuids(
		IntPtr encoderSession,
		NvencNative.NvEncGetEncodePresetGUIDsDelegate getPresetGuids,
		Guid codecGuid,
		uint presetCount,
		out Guid selectedPresetGuid)
	{
		selectedPresetGuid = Guid.Empty;
		var codecPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
		var guidSize = Marshal.SizeOf<Guid>();
		var presetGuidBuffer = Marshal.AllocHGlobal((int)(presetCount * (uint)guidSize));
		try
		{
			Marshal.StructureToPtr(codecGuid, codecPtr, false);
			var rc = getPresetGuids(encoderSession, codecPtr, presetGuidBuffer, presetCount, out var copied);
			if (rc != 0)
			{
				return rc;
			}

			if (copied > 0)
			{
				selectedPresetGuid = Marshal.PtrToStructure<Guid>(presetGuidBuffer);
			}

			return rc;
		}
		finally
		{
			Marshal.FreeHGlobal(codecPtr);
			Marshal.FreeHGlobal(presetGuidBuffer);
		}
	}
}
