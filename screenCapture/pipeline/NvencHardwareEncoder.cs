using System.Runtime.InteropServices;

public sealed class NvencHardwareEncoder : IHardwareEncoder
{
	private const int BitstreamBufferCount = 4;
	private readonly Dictionary<nint, nint> _registeredResources = new();
	public static NvencReadiness ProbeReadiness()
	{
		if (!GpuCapabilityProbe.IsNvidiaAdapterPresent())
		{
			return new NvencReadiness(false, "nvidia_adapter_not_detected", 0, 0, 0, false, false, false, false);
		}

		IntPtr nvenc = IntPtr.Zero;
		IntPtr cuda = IntPtr.Zero;
		IntPtr fnList = IntPtr.Zero;
		try
		{
			if (!NativeNvencProbe.TryLoad(out nvenc, out var msg))
			{
				return new NvencReadiness(false, $"probe_failed:{msg}", 0, 0, 0, false, false, false, false);
			}

			if (!NativeNvencProbe.TryBindCreateInstance(nvenc, out var createInstance, out msg) ||
				createInstance == null)
			{
				return new NvencReadiness(false, $"bind_failed:{msg}", 0, 0, 0, false, false, false, false);
			}

			if (!NativeNvencProbe.TryBindGetMaxSupportedVersion(nvenc, out var getMaxVersion, out msg) ||
				getMaxVersion == null)
			{
				return new NvencReadiness(false, $"bind_failed:{msg}", 0, 0, 0, false, false, false, false);
			}

			var maxRc = getMaxVersion(out var maxVersion);
			if (maxRc != 0)
			{
				return new NvencReadiness(false, $"max_version_query_failed:{NvencNative.ResultToString(maxRc)}", 0, 0, 0, false, false, false, false);
			}

			if (!NvencNative.IsApiCompatible(maxVersion, NvencNative.NVENCAPI_VERSION))
			{
				return new NvencReadiness(false, $"api_version_too_old:max=0x{maxVersion:X8};required=0x{NvencNative.NVENCAPI_VERSION:X8}", maxVersion, 0, 0, false, false, false, false);
			}

			fnList = NvencFunctionList.Allocate(maxVersion, out _);
			var ciRc = createInstance(fnList);
			if (ciRc != 0)
			{
				return new NvencReadiness(false, $"create_instance_failed:{NvencNative.ResultToString(ciRc)}", maxVersion, 0, 0, false, false, false, false);
			}

			var fnPtrCount = NvencFunctionList.CountNonZeroPointerSlots(fnList, 96);
			if (fnPtrCount == 0)
			{
				return new NvencReadiness(false, "create_instance_empty_function_list", maxVersion, 0, 0, false, false, false, false);
			}

			var requiredSlots = NvencFunctionListInspector.ReadRequiredSlots(fnList);
			if (!requiredSlots.AllPresent)
			{
				return new NvencReadiness(false, "required_function_slots_missing", maxVersion, 0, fnPtrCount, false, false, false, false);
			}

			var openSessionPtr = NvencFunctionListInspector.ReadPointerAtSlot(fnList, 0);
			if (!NvencApiBootstrap.TryBindOpenSessionDelegate(openSessionPtr, out var openSessionDelegate, out msg) ||
				openSessionDelegate == null)
			{
				return new NvencReadiness(false, $"open_session_bind_failed:{msg}", maxVersion, 0, fnPtrCount, true, false, false, false);
			}

			var initializePtr = NvencFunctionListInspector.ReadPointerAtSlot(fnList, 12);
			if (!NvencApiBootstrap.TryBindInitializeEncoderDelegate(initializePtr, out var initializeDelegate, out msg) ||
				initializeDelegate == null)
			{
				return new NvencReadiness(false, $"initialize_encoder_bind_failed:{msg}", maxVersion, 0, fnPtrCount, true, true, false, false);
			}

			var presetCountPtr = NvencFunctionListInspector.ReadPointerAtSlot(fnList, 8);
			var presetGuidsPtr = NvencFunctionListInspector.ReadPointerAtSlot(fnList, 9);
			if (!NvencApiBootstrap.TryBindGetEncodePresetCountDelegate(presetCountPtr, out var presetCountDelegate, out msg) ||
				presetCountDelegate == null)
			{
				return new NvencReadiness(false, $"preset_count_bind_failed:{msg}", maxVersion, 0, fnPtrCount, true, true, true, false);
			}

			if (!NvencApiBootstrap.TryBindGetEncodePresetGuidsDelegate(presetGuidsPtr, out var presetGuidsDelegate, out msg) ||
				presetGuidsDelegate == null)
			{
				return new NvencReadiness(false, $"preset_guids_bind_failed:{msg}", maxVersion, 0, fnPtrCount, true, true, true, false);
			}

			if (!NativeNvencProbe.TryLoadCuda(out cuda, out msg))
			{
				return new NvencReadiness(false, $"cuda_missing:{msg}", maxVersion, 0, fnPtrCount, true, true, true, true);
			}

			if (!NativeNvencProbe.TryBindCudaInit(cuda, out var cuInit, out msg) || cuInit == null)
			{
				return new NvencReadiness(false, $"cuda_bind_failed:{msg}", maxVersion, 0, fnPtrCount, true, true, true, true);
			}

			if (!NativeNvencProbe.TryBindCudaDriverGetVersion(cuda, out var cuGetVersion, out msg) || cuGetVersion == null)
			{
				return new NvencReadiness(false, $"cuda_bind_failed:{msg}", maxVersion, 0, fnPtrCount, true, true, true, true);
			}

			var cuInitRc = cuInit(0);
			if (cuInitRc != 0)
			{
				return new NvencReadiness(false, $"cuda_init_failed:{NvencNative.CudaResultToString(cuInitRc)}", maxVersion, 0, fnPtrCount, true, true, true, true);
			}

			var cuVersionRc = cuGetVersion(out var cudaVersion);
			if (cuVersionRc != 0)
			{
				return new NvencReadiness(false, $"cuda_version_failed:{NvencNative.CudaResultToString(cuVersionRc)}", maxVersion, 0, fnPtrCount, true, true, true, true);
			}

			return new NvencReadiness(true, "ready", maxVersion, cudaVersion, fnPtrCount, true, true, true, true);
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
	private IntPtr _createBitstreamBufferPtr;
	private IntPtr _destroyBitstreamBufferPtr;
	private IntPtr _encodePicturePtr;
	private IntPtr _lockBitstreamPtr;
	private IntPtr _unlockBitstreamPtr;
	private IntPtr _mapInputResourcePtr;
	private IntPtr _unmapInputResourcePtr;
	private IntPtr _registerResourcePtr;
	private IntPtr _unregisterResourcePtr;
	private IntPtr _destroyEncoderPtr;
	private NvencNative.NvEncGetEncodeGuidCountDelegate? _getEncodeGuidCount;
	private NvencNative.NvEncGetEncodeGUIDsDelegate? _getEncodeGuids;
	private NvencNative.NvEncGetEncodeProfileGuidCountDelegate? _getEncodeProfileGuidCount;
	private NvencNative.NvEncGetEncodeProfileGUIDsDelegate? _getEncodeProfileGuids;
	private NvencNative.NvEncGetEncodePresetCountDelegate? _getEncodePresetCount;
	private NvencNative.NvEncGetEncodePresetGUIDsDelegate? _getEncodePresetGuids;
	private NvencNative.NvEncInitializeEncoderDelegate? _initializeEncoder;
	private NvencNative.NvEncCreateBitstreamBufferDelegate? _createBitstreamBuffer;
	private NvencNative.NvEncDestroyBitstreamBufferDelegate? _destroyBitstreamBuffer;
	private NvencNative.NvEncEncodePictureDelegate? _encodePicture;
	private NvencNative.NvEncLockBitstreamDelegate? _lockBitstream;
	private NvencNative.NvEncUnlockBitstreamDelegate? _unlockBitstream;
	private NvencNative.NvEncMapInputResourceDelegate? _mapInputResource;
	private NvencNative.NvEncUnmapInputResourceDelegate? _unmapInputResource;
	private NvencNative.NvEncRegisterResourceDelegate? _registerResource;
	private NvencNative.NvEncUnregisterResourceDelegate? _unregisterResource;
	private NvencNative.NvEncDestroyEncoderDelegate? _destroyEncoder;
	private int _initializeEncoderRc;
	private IntPtr[] _bitstreamBuffers = Array.Empty<IntPtr>();
	private int _bitstreamBufferCreateFailures;
	private int _bitstreamBufferDestroyFailures;
	private int _destroyEncoderRc;
	private int _registerResourceFailures;
	private int _unregisterResourceFailures;
	private int _mappedResourceFailures;
	private int _unmappedResourceFailures;
	private long _registeredFrameCount;
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
	private int _getEncodeProfileGuidsRc;
	private bool _supportsH264;
	private bool _supportsHevc;
	private Guid _selectedCodecGuid;
	private string _selectedCodecName = "unknown";
	private Guid _selectedPresetGuid;
	private Guid _selectedProfileGuid;

	public void Start(RecordingSettings settings)
	{
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
		_createBitstreamBufferPtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 15);
		_destroyBitstreamBufferPtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 16);
		_encodePicturePtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 17);
		_lockBitstreamPtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 18);
		_unlockBitstreamPtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 19);
		_mapInputResourcePtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 26);
		_unmapInputResourcePtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 27);
		_destroyEncoderPtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 28);
		_registerResourcePtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 32);
		_unregisterResourcePtr = NvencFunctionListInspector.ReadPointerAtSlot(_functionListBuffer, 33);

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
		if (!NvencApiBootstrap.TryBindGetEncodePresetCountDelegate(
			_getEncodePresetCountPtr,
			out _getEncodePresetCount,
			out openPtrMsg))
		{
			_status = $"get_encode_preset_count_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC get-encode-preset-count bind failed: {openPtrMsg}");
		}
		if (!NvencApiBootstrap.TryBindGetEncodePresetGuidsDelegate(
			_getEncodePresetGuidsPtr,
			out _getEncodePresetGuids,
			out openPtrMsg))
		{
			_status = $"get_encode_preset_guids_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC get-encode-preset-guids bind failed: {openPtrMsg}");
		}

		if (!NvencApiBootstrap.TryBindInitializeEncoderDelegate(
			_initializeEncoderPtr,
			out _initializeEncoder,
			out openPtrMsg))
		{
			_status = $"initialize_encoder_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC initialize-encoder bind failed: {openPtrMsg}");
		}
		if (!NvencApiBootstrap.TryBindDelegate(
			_createBitstreamBufferPtr,
			out _createBitstreamBuffer,
			out openPtrMsg))
		{
			_status = $"create_bitstream_buffer_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC create-bitstream-buffer bind failed: {openPtrMsg}");
		}
		if (!NvencApiBootstrap.TryBindDelegate(
			_destroyBitstreamBufferPtr,
			out _destroyBitstreamBuffer,
			out openPtrMsg))
		{
			_status = $"destroy_bitstream_buffer_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC destroy-bitstream-buffer bind failed: {openPtrMsg}");
		}
		if (!NvencApiBootstrap.TryBindDelegate(
			_encodePicturePtr,
			out _encodePicture,
			out openPtrMsg))
		{
			_status = $"encode_picture_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC encode-picture bind failed: {openPtrMsg}");
		}
		if (!NvencApiBootstrap.TryBindDelegate(
			_lockBitstreamPtr,
			out _lockBitstream,
			out openPtrMsg))
		{
			_status = $"lock_bitstream_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC lock-bitstream bind failed: {openPtrMsg}");
		}
		if (!NvencApiBootstrap.TryBindDelegate(
			_unlockBitstreamPtr,
			out _unlockBitstream,
			out openPtrMsg))
		{
			_status = $"unlock_bitstream_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC unlock-bitstream bind failed: {openPtrMsg}");
		}
		if (!NvencApiBootstrap.TryBindDelegate(
			_mapInputResourcePtr,
			out _mapInputResource,
			out openPtrMsg))
		{
			_status = $"map_input_resource_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC map-input-resource bind failed: {openPtrMsg}");
		}
		if (!NvencApiBootstrap.TryBindDelegate(
			_unmapInputResourcePtr,
			out _unmapInputResource,
			out openPtrMsg))
		{
			_status = $"unmap_input_resource_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC unmap-input-resource bind failed: {openPtrMsg}");
		}
		if (!NvencApiBootstrap.TryBindDelegate(
			_registerResourcePtr,
			out _registerResource,
			out openPtrMsg))
		{
			_status = $"register_resource_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC register-resource bind failed: {openPtrMsg}");
		}
		if (!NvencApiBootstrap.TryBindDelegate(
			_unregisterResourcePtr,
			out _unregisterResource,
			out openPtrMsg))
		{
			_status = $"unregister_resource_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC unregister-resource bind failed: {openPtrMsg}");
		}
		if (!NvencApiBootstrap.TryBindDelegate(
			_destroyEncoderPtr,
			out _destroyEncoder,
			out openPtrMsg))
		{
			_status = $"destroy_encoder_bind_failed:{openPtrMsg}";
			throw new NotSupportedException($"NVENC destroy-encoder bind failed: {openPtrMsg}");
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

		if (_getEncodeProfileGuids == null)
		{
			_status = "get_encode_profile_guids_delegate_null";
			throw new NotSupportedException("NVENC get encode profile GUIDs delegate is null.");
		}

		_getEncodeProfileGuidsRc = QueryProfileGuids(
			_encoderSession,
			_getEncodeProfileGuids,
			_selectedCodecGuid,
			out _selectedProfileGuid);
		if (_getEncodeProfileGuidsRc != 0)
		{
			_status = $"get_encode_profile_guids_failed:{NvencNative.ResultToString(_getEncodeProfileGuidsRc)}";
			throw new NotSupportedException($"NVENC get encode profile guids failed: {NvencNative.ResultToString(_getEncodeProfileGuidsRc)}");
		}
		if (_selectedProfileGuid == Guid.Empty)
		{
			_status = "selected_profile_guid_empty";
			throw new NotSupportedException("NVENC profile enumeration returned no selectable profile GUID.");
		}

		var getPresetCount = _getEncodePresetCount;
		var getPresetGuids = _getEncodePresetGuids;
		if (getPresetCount == null || getPresetGuids == null)
		{
			_status = "preset_query_delegate_null";
			throw new NotSupportedException("NVENC preset query delegates are null.");
		}

		_getEncodePresetCountRc = QueryPresetGuidCount(
			_encoderSession,
			getPresetCount,
			_selectedCodecGuid,
			out _presetGuidCount);
		if (_getEncodePresetCountRc != 0)
		{
			_status = $"get_encode_preset_count_failed:{NvencNative.ResultToString(_getEncodePresetCountRc)}";
			throw new NotSupportedException($"NVENC get encode preset count failed: {NvencNative.ResultToString(_getEncodePresetCountRc)}");
		}

		if (_presetGuidCount == 0)
		{
			_status = "get_encode_preset_count_zero";
			throw new NotSupportedException("NVENC reported zero presets for selected codec.");
		}

		_getEncodePresetGuidsRc = QueryPresetGuids(
			_encoderSession,
			getPresetGuids,
			_selectedCodecGuid,
			_presetGuidCount,
			out _selectedPresetGuid);
		if (_getEncodePresetGuidsRc != 0)
		{
			_status = $"get_encode_preset_guids_failed:{NvencNative.ResultToString(_getEncodePresetGuidsRc)}";
			throw new NotSupportedException($"NVENC get encode preset guids failed: {NvencNative.ResultToString(_getEncodePresetGuidsRc)}");
		}

		if (_selectedPresetGuid == Guid.Empty)
		{
			_status = "selected_preset_guid_empty";
			throw new NotSupportedException("NVENC preset enumeration returned no selectable preset GUID.");
		}

		var initializeEncoder = _initializeEncoder;
		if (initializeEncoder == null)
		{
			_status = "initialize_encoder_delegate_null";
			throw new NotSupportedException("NVENC initialize encoder delegate is null.");
		}

		var encodeWidth = NormalizeDimension(settings.Width);
		var encodeHeight = NormalizeDimension(settings.Height);
		var fpsNum = (uint)Math.Max(1, settings.Fps);
		var bitrate = (uint)Math.Max(1, settings.Bitrate);
		var config = new NvencNative.NV_ENC_CONFIG
		{
			version = NvencNative.EncodeStructVersion<NvencNative.NV_ENC_CONFIG>(NvencNative.NV_ENC_CONFIG_VER),
			reserved0 = bitrate
		};
		var configPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NvencNative.NV_ENC_CONFIG>());
		var initPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NvencNative.NV_ENC_INITIALIZE_PARAMS>());
		try
		{
			Marshal.StructureToPtr(config, configPtr, false);
			var init = new NvencNative.NV_ENC_INITIALIZE_PARAMS
			{
				version = NvencNative.EncodeStructVersion<NvencNative.NV_ENC_INITIALIZE_PARAMS>(NvencNative.NV_ENC_INITIALIZE_PARAMS_VER),
				encodeGUID = _selectedCodecGuid,
				presetGUID = _selectedPresetGuid,
				encodeWidth = encodeWidth,
				encodeHeight = encodeHeight,
				darWidth = encodeWidth,
				darHeight = encodeHeight,
				frameRateNum = fpsNum,
				frameRateDen = 1,
				enableEncodeAsync = 1,
				enablePTD = 1,
				encodeConfig = configPtr,
				maxEncodeWidth = encodeWidth,
				maxEncodeHeight = encodeHeight
			};
			Marshal.StructureToPtr(init, initPtr, false);
			_initializeEncoderRc = initializeEncoder(_encoderSession, initPtr);
		}
		finally
		{
			Marshal.FreeHGlobal(initPtr);
			Marshal.FreeHGlobal(configPtr);
		}

		if (_initializeEncoderRc != 0)
		{
			_status = $"initialize_encoder_failed:{NvencNative.ResultToString(_initializeEncoderRc)}";
			throw new NotSupportedException($"NVENC initialize encoder failed: {NvencNative.ResultToString(_initializeEncoderRc)}");
		}

		InitializeBitstreamBufferPool();

		_status = $"open_session_ok_fnptrs={_functionPointerCount}_cuda={FormatCudaDriverVersion(_cudaDriverVersion)}_maxver=0x{_maxSupportedVersion:X8}({FormatVersionWords(_maxSupportedVersion)})_fnlist=0x{_functionListVersion:X8}_session=0x{_encoderSession.ToInt64():X}_codecCount={_encodeGuidCount}_h264={_supportsH264}_hevc={_supportsHevc}_h264Profiles={_h264ProfileGuidCount}_hevcProfiles={_hevcProfileGuidCount}_selectedCodec={_selectedCodecName}_selectedProfileGuid={_selectedProfileGuid}_presetCount={_presetGuidCount}_selectedPresetGuid={_selectedPresetGuid}_initBound={(_initializeEncoder != null ? 1 : 0)}_initRc={NvencNative.ResultToString(_initializeEncoderRc)}_bitstreamBuffers={_bitstreamBuffers.Length}_coreEncodeApiBound={(_createBitstreamBuffer != null && _destroyBitstreamBuffer != null && _encodePicture != null && _lockBitstream != null && _unlockBitstream != null && _mapInputResource != null && _unmapInputResource != null && _registerResource != null && _unregisterResource != null && _destroyEncoder != null ? 1 : 0)}_but_not_implemented";
		throw new NotImplementedException(
			"NVENC runtime detected, but native session creation/encode path is not implemented yet.");
	}

	public void Encode(TextureFrameRef frame)
	{
		if (_encoderSession == IntPtr.Zero)
		{
			return;
		}

		if (!EnsureRegisteredResource(frame, out var registeredResource))
		{
			return;
		}

		var mapInput = _mapInputResource;
		var unmapInput = _unmapInputResource;
		if (mapInput == null || unmapInput == null)
		{
			return;
		}

		var mapParams = new NvencNative.NV_ENC_MAP_INPUT_RESOURCE
		{
			version = NvencNative.EncodeStructVersion<NvencNative.NV_ENC_MAP_INPUT_RESOURCE>(NvencNative.NV_ENC_MAP_INPUT_RESOURCE_VER),
			registeredResource = registeredResource
		};
		var mapPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NvencNative.NV_ENC_MAP_INPUT_RESOURCE>());
		try
		{
			Marshal.StructureToPtr(mapParams, mapPtr, false);
			var mapRc = mapInput(_encoderSession, mapPtr);
			if (mapRc != 0)
			{
				_mappedResourceFailures++;
				return;
			}

			var mapped = Marshal.PtrToStructure<NvencNative.NV_ENC_MAP_INPUT_RESOURCE>(mapPtr);
			if (mapped.mappedResource == IntPtr.Zero)
			{
				_mappedResourceFailures++;
				return;
			}

			var unmapRc = unmapInput(_encoderSession, mapped.mappedResource);
			if (unmapRc != 0)
			{
				_unmappedResourceFailures++;
			}
		}
		finally
		{
			Marshal.FreeHGlobal(mapPtr);
		}
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
		return $"{_status};maxVersion=0x{_maxSupportedVersion:X8}({FormatVersionWords(_maxSupportedVersion)});cudaDriver={FormatCudaDriverVersion(_cudaDriverVersion)};fnListVersion=0x{_functionListVersion:X8};createInstanceRc={NvencNative.ResultToString(_createInstanceRc)};fnPtrCount={_functionPointerCount};openSessionPtr=0x{_openSessionPtr.ToInt64():X};openSessionBound={(_openSession != null ? 1 : 0)};openSessionRc={NvencNative.ResultToString(_openSessionRc)};encoderSession=0x{_encoderSession.ToInt64():X};getEncodeGuidCountPtr=0x{_getEncodeGuidCountPtr.ToInt64():X};getEncodeGuidCountBound={(_getEncodeGuidCount != null ? 1 : 0)};getEncodeGuidCountRc={NvencNative.ResultToString(_getEncodeGuidCountRc)};getEncodeGuidsPtr=0x{_getEncodeGuidsPtr.ToInt64():X};getEncodeGuidsBound={(_getEncodeGuids != null ? 1 : 0)};getEncodeGuidsRc={NvencNative.ResultToString(_getEncodeGuidsRc)};codecCount={_encodeGuidCount};supportsH264={_supportsH264};supportsHevc={_supportsHevc};selectedCodec={_selectedCodecName};selectedCodecGuid={_selectedCodecGuid};selectedProfileGuid={_selectedProfileGuid};getEncodeProfileGuidCountPtr=0x{_getEncodeProfileGuidCountPtr.ToInt64():X};getEncodeProfileGuidCountBound={(_getEncodeProfileGuidCount != null ? 1 : 0)};getEncodeProfileGuidsPtr=0x{_getEncodeProfileGuidsPtr.ToInt64():X};getEncodeProfileGuidsBound={(_getEncodeProfileGuids != null ? 1 : 0)};getEncodeProfileGuidsRc={NvencNative.ResultToString(_getEncodeProfileGuidsRc)};getEncodePresetCountPtr=0x{_getEncodePresetCountPtr.ToInt64():X};getEncodePresetCountBound={(_getEncodePresetCount != null ? 1 : 0)};getEncodePresetCountRc={NvencNative.ResultToString(_getEncodePresetCountRc)};getEncodePresetGuidsPtr=0x{_getEncodePresetGuidsPtr.ToInt64():X};getEncodePresetGuidsBound={(_getEncodePresetGuids != null ? 1 : 0)};getEncodePresetGuidsRc={NvencNative.ResultToString(_getEncodePresetGuidsRc)};presetCount={_presetGuidCount};selectedPresetGuid={_selectedPresetGuid};initializeEncoderPtr=0x{_initializeEncoderPtr.ToInt64():X};initializeEncoderBound={(_initializeEncoder != null ? 1 : 0)};initializeEncoderRc={NvencNative.ResultToString(_initializeEncoderRc)};createBitstreamBufferPtr=0x{_createBitstreamBufferPtr.ToInt64():X};createBitstreamBufferBound={(_createBitstreamBuffer != null ? 1 : 0)};destroyBitstreamBufferPtr=0x{_destroyBitstreamBufferPtr.ToInt64():X};destroyBitstreamBufferBound={(_destroyBitstreamBuffer != null ? 1 : 0)};bitstreamBufferCount={_bitstreamBuffers.Length};bitstreamBufferCreateFailures={_bitstreamBufferCreateFailures};bitstreamBufferDestroyFailures={_bitstreamBufferDestroyFailures};encodePicturePtr=0x{_encodePicturePtr.ToInt64():X};encodePictureBound={(_encodePicture != null ? 1 : 0)};lockBitstreamPtr=0x{_lockBitstreamPtr.ToInt64():X};lockBitstreamBound={(_lockBitstream != null ? 1 : 0)};unlockBitstreamPtr=0x{_unlockBitstreamPtr.ToInt64():X};unlockBitstreamBound={(_unlockBitstream != null ? 1 : 0)};mapInputResourcePtr=0x{_mapInputResourcePtr.ToInt64():X};mapInputResourceBound={(_mapInputResource != null ? 1 : 0)};unmapInputResourcePtr=0x{_unmapInputResourcePtr.ToInt64():X};unmapInputResourceBound={(_unmapInputResource != null ? 1 : 0)};registerResourcePtr=0x{_registerResourcePtr.ToInt64():X};registerResourceBound={(_registerResource != null ? 1 : 0)};unregisterResourcePtr=0x{_unregisterResourcePtr.ToInt64():X};unregisterResourceBound={(_unregisterResource != null ? 1 : 0)};registeredResourceCount={_registeredResources.Count};registeredFrameCount={_registeredFrameCount};registerResourceFailures={_registerResourceFailures};unregisterResourceFailures={_unregisterResourceFailures};mapResourceFailures={_mappedResourceFailures};unmapResourceFailures={_unmappedResourceFailures};destroyEncoderPtr=0x{_destroyEncoderPtr.ToInt64():X};destroyEncoderBound={(_destroyEncoder != null ? 1 : 0)};destroyEncoderRc={NvencNative.ResultToString(_destroyEncoderRc)};h264ProfileCount={_h264ProfileGuidCount};h264ProfileCountRc={NvencNative.ResultToString(_getEncodeProfileGuidCountH264Rc)};hevcProfileCount={_hevcProfileGuidCount};hevcProfileCountRc={NvencNative.ResultToString(_getEncodeProfileGuidCountHevcRc)}";
	}

	public void Stop()
	{
	}

	public void Dispose()
	{
		DestroyBitstreamBuffers();
		UnregisterAllResources();
		DestroyEncoderSession();

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
		_createBitstreamBufferPtr = IntPtr.Zero;
		_destroyBitstreamBufferPtr = IntPtr.Zero;
		_encodePicturePtr = IntPtr.Zero;
		_lockBitstreamPtr = IntPtr.Zero;
		_unlockBitstreamPtr = IntPtr.Zero;
		_mapInputResourcePtr = IntPtr.Zero;
		_unmapInputResourcePtr = IntPtr.Zero;
		_registerResourcePtr = IntPtr.Zero;
		_unregisterResourcePtr = IntPtr.Zero;
		_destroyEncoderPtr = IntPtr.Zero;
		_getEncodeGuidCount = null;
		_getEncodeGuids = null;
		_getEncodeProfileGuidCount = null;
		_getEncodeProfileGuids = null;
		_getEncodePresetCount = null;
		_getEncodePresetGuids = null;
		_initializeEncoder = null;
		_createBitstreamBuffer = null;
		_destroyBitstreamBuffer = null;
		_encodePicture = null;
		_lockBitstream = null;
		_unlockBitstream = null;
		_mapInputResource = null;
		_unmapInputResource = null;
		_registerResource = null;
		_unregisterResource = null;
		_destroyEncoder = null;
		_getEncodeGuidCountRc = 0;
		_getEncodeGuidsRc = 0;
		_encodeGuidCount = 0;
		_getEncodeProfileGuidCountH264Rc = 0;
		_getEncodeProfileGuidCountHevcRc = 0;
		_getEncodeProfileGuidsRc = 0;
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
		_selectedProfileGuid = Guid.Empty;
		_maxSupportedVersion = 0;
		_cudaDriverVersion = 0;
		_createInstanceRc = 0;
		_initializeEncoderRc = 0;
		_bitstreamBuffers = Array.Empty<IntPtr>();
		_bitstreamBufferCreateFailures = 0;
		_bitstreamBufferDestroyFailures = 0;
		_destroyEncoderRc = 0;
		_registeredResources.Clear();
		_registerResourceFailures = 0;
		_unregisterResourceFailures = 0;
		_mappedResourceFailures = 0;
		_unmappedResourceFailures = 0;
		_registeredFrameCount = 0;
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

	private static uint NormalizeDimension(int value)
	{
		var v = Math.Max(2, value);
		return (uint)(v & ~1);
	}

	private void InitializeBitstreamBufferPool()
	{
		var createBitstream = _createBitstreamBuffer;
		if (createBitstream == null)
		{
			_status = "create_bitstream_buffer_delegate_null";
			throw new NotSupportedException("NVENC create bitstream buffer delegate is null.");
		}

		var buffers = new IntPtr[BitstreamBufferCount];
		for (var i = 0; i < buffers.Length; i++)
		{
			var createParams = new NvencNative.NV_ENC_CREATE_BITSTREAM_BUFFER
			{
				version = NvencNative.EncodeStructVersion<NvencNative.NV_ENC_CREATE_BITSTREAM_BUFFER>(NvencNative.NV_ENC_CREATE_BITSTREAM_BUFFER_VER),
				size = 2 * 1024 * 1024
			};
			var createPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NvencNative.NV_ENC_CREATE_BITSTREAM_BUFFER>());
			try
			{
				Marshal.StructureToPtr(createParams, createPtr, false);
				var rc = createBitstream(_encoderSession, createPtr);
				if (rc != 0)
				{
					_bitstreamBufferCreateFailures++;
					_status = $"create_bitstream_buffer_failed:index={i};rc={NvencNative.ResultToString(rc)}";
					throw new NotSupportedException($"NVENC create bitstream buffer failed at index {i}: {NvencNative.ResultToString(rc)}");
				}

				var created = Marshal.PtrToStructure<NvencNative.NV_ENC_CREATE_BITSTREAM_BUFFER>(createPtr);
				if (created.bitstreamBuffer == IntPtr.Zero)
				{
					_bitstreamBufferCreateFailures++;
					_status = $"create_bitstream_buffer_empty:index={i}";
					throw new NotSupportedException($"NVENC created empty bitstream buffer at index {i}.");
				}

				buffers[i] = created.bitstreamBuffer;
			}
			finally
			{
				Marshal.FreeHGlobal(createPtr);
			}
		}

		_bitstreamBuffers = buffers;
	}

	private void DestroyBitstreamBuffers()
	{
		var destroyBitstream = _destroyBitstreamBuffer;
		if (destroyBitstream == null || _encoderSession == IntPtr.Zero || _bitstreamBuffers.Length == 0)
		{
			_bitstreamBuffers = Array.Empty<IntPtr>();
			return;
		}

		for (var i = 0; i < _bitstreamBuffers.Length; i++)
		{
			var buffer = _bitstreamBuffers[i];
			if (buffer == IntPtr.Zero)
			{
				continue;
			}

			var rc = destroyBitstream(_encoderSession, buffer);
			if (rc != 0)
			{
				_bitstreamBufferDestroyFailures++;
			}
		}

		_bitstreamBuffers = Array.Empty<IntPtr>();
	}

	private void DestroyEncoderSession()
	{
		if (_encoderSession == IntPtr.Zero)
		{
			return;
		}

		var destroyEncoder = _destroyEncoder;
		if (destroyEncoder != null)
		{
			_destroyEncoderRc = destroyEncoder(_encoderSession);
		}

		_encoderSession = IntPtr.Zero;
	}

	private bool EnsureRegisteredResource(TextureFrameRef frame, out IntPtr registeredResource)
	{
		registeredResource = IntPtr.Zero;
		var texturePtr = frame.Texture.NativePointer;
		if (texturePtr == IntPtr.Zero)
		{
			return false;
		}

		if (_registeredResources.TryGetValue(texturePtr, out var existing))
		{
			registeredResource = existing;
			return true;
		}

		var register = _registerResource;
		if (register == null)
		{
			return false;
		}

		var registerParams = new NvencNative.NV_ENC_REGISTER_RESOURCE
		{
			version = NvencNative.EncodeStructVersion<NvencNative.NV_ENC_REGISTER_RESOURCE>(NvencNative.NV_ENC_REGISTER_RESOURCE_VER),
			resourceType = NvencNative.NV_ENC_INPUT_RESOURCE_TYPE_DIRECTX,
			width = (uint)frame.Width,
			height = (uint)frame.Height,
			resourceToRegister = texturePtr,
			bufferFormat = NvencNative.NV_ENC_BUFFER_FORMAT_ARGB
		};
		var registerPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NvencNative.NV_ENC_REGISTER_RESOURCE>());
		try
		{
			Marshal.StructureToPtr(registerParams, registerPtr, false);
			var rc = register(_encoderSession, registerPtr);
			if (rc != 0)
			{
				_registerResourceFailures++;
				return false;
			}

			var created = Marshal.PtrToStructure<NvencNative.NV_ENC_REGISTER_RESOURCE>(registerPtr);
			if (created.registeredResource == IntPtr.Zero)
			{
				_registerResourceFailures++;
				return false;
			}

			registeredResource = created.registeredResource;
			_registeredResources[texturePtr] = registeredResource;
			_registeredFrameCount++;
			return true;
		}
		finally
		{
			Marshal.FreeHGlobal(registerPtr);
		}
	}

	private void UnregisterAllResources()
	{
		var unregister = _unregisterResource;
		if (unregister == null || _registeredResources.Count == 0 || _encoderSession == IntPtr.Zero)
		{
			_registeredResources.Clear();
			return;
		}

		foreach (var kv in _registeredResources)
		{
			var rc = unregister(_encoderSession, kv.Value);
			if (rc != 0)
			{
				_unregisterResourceFailures++;
			}
		}

		_registeredResources.Clear();
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

	private static int QueryProfileGuids(
		IntPtr encoderSession,
		NvencNative.NvEncGetEncodeProfileGUIDsDelegate getProfileGuids,
		Guid codecGuid,
		out Guid selectedProfileGuid)
	{
		selectedProfileGuid = Guid.Empty;
		var codecPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
		try
		{
			Marshal.StructureToPtr(codecGuid, codecPtr, false);
			var profileCountRc = getProfileGuids(encoderSession, codecPtr, IntPtr.Zero, 0, out var profileCount);
			if (profileCountRc != 0 || profileCount == 0)
			{
				return profileCountRc;
			}

			var guidSize = Marshal.SizeOf<Guid>();
			var profileBuffer = Marshal.AllocHGlobal((int)(profileCount * (uint)guidSize));
			try
			{
				var rc = getProfileGuids(encoderSession, codecPtr, profileBuffer, profileCount, out var copied);
				if (rc != 0)
				{
					return rc;
				}

				if (copied > 0)
				{
					selectedProfileGuid = Marshal.PtrToStructure<Guid>(profileBuffer);
				}

				return rc;
			}
			finally
			{
				Marshal.FreeHGlobal(profileBuffer);
			}
		}
		finally
		{
			Marshal.FreeHGlobal(codecPtr);
		}
	}
}
