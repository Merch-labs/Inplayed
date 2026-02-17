using System.Runtime.InteropServices;

internal static class NvencNative
{
	public static readonly Guid NV_ENC_CODEC_H264_GUID = new("6BC82762-4E63-4CA4-AA85-1E50F321F6BF");
	public static readonly Guid NV_ENC_CODEC_HEVC_GUID = new("790CDC88-4522-4D7B-9425-BDA9975F7603");

	public const uint NV_ENC_SUCCESS = 0;
	public const uint NV_ENC_ERR_NO_ENCODE_DEVICE = 2;
	public const uint NV_ENC_ERR_UNSUPPORTED_DEVICE = 4;
	public const uint NV_ENC_ERR_INVALID_VERSION = 15;
	public const uint NV_ENC_ERR_OUT_OF_MEMORY = 10;

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncodeApiCreateInstanceDelegate(IntPtr functionList);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncodeApiGetMaxSupportedVersionDelegate(out uint version);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncOpenEncodeSessionExDelegate(IntPtr openSessionExParams, out IntPtr encoderSession);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncGetEncodeGuidCountDelegate(IntPtr encoderSession, out uint guidCount);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncGetEncodeGUIDsDelegate(
		IntPtr encoderSession,
		IntPtr guids,
		uint guidArraySize,
		out uint guidCount);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncGetEncodeProfileGuidCountDelegate(IntPtr encoderSession, IntPtr encodeGuid, out uint profileGuidCount);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncGetEncodeProfileGUIDsDelegate(
		IntPtr encoderSession,
		IntPtr encodeGuid,
		IntPtr profileGuids,
		uint guidArraySize,
		out uint guidCount);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncInitializeEncoderDelegate(IntPtr encoderSession, IntPtr createEncodeParams);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncGetEncodePresetCountDelegate(IntPtr encoderSession, IntPtr encodeGuid, out uint presetCount);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncGetEncodePresetGUIDsDelegate(
		IntPtr encoderSession,
		IntPtr encodeGuid,
		IntPtr presetGuids,
		uint guidArraySize,
		out uint guidCount);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncCreateBitstreamBufferDelegate(IntPtr encoderSession, IntPtr createBitstreamBufferParams);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncDestroyBitstreamBufferDelegate(IntPtr encoderSession, IntPtr bitstreamBuffer);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncEncodePictureDelegate(IntPtr encoderSession, IntPtr picParams);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncLockBitstreamDelegate(IntPtr encoderSession, IntPtr lockBitstreamParams);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncUnlockBitstreamDelegate(IntPtr encoderSession, IntPtr bitstreamBuffer);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncRegisterResourceDelegate(IntPtr encoderSession, IntPtr registerResourceParams);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncUnregisterResourceDelegate(IntPtr encoderSession, IntPtr registeredResource);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncMapInputResourceDelegate(IntPtr encoderSession, IntPtr mapInputResourceParams);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncUnmapInputResourceDelegate(IntPtr encoderSession, IntPtr mappedInputBuffer);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncDestroyEncoderDelegate(IntPtr encoderSession);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate int CuInitDelegate(uint flags);

	[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
	public delegate int CuDriverGetVersionDelegate(out int driverVersion);

	public static string ResultToString(int code)
	{
		var value = unchecked((uint)code);
		return value switch
		{
			NV_ENC_SUCCESS => "NV_ENC_SUCCESS",
			NV_ENC_ERR_NO_ENCODE_DEVICE => "NV_ENC_ERR_NO_ENCODE_DEVICE",
			NV_ENC_ERR_UNSUPPORTED_DEVICE => "NV_ENC_ERR_UNSUPPORTED_DEVICE",
			NV_ENC_ERR_INVALID_VERSION => "NV_ENC_ERR_INVALID_VERSION",
			NV_ENC_ERR_OUT_OF_MEMORY => "NV_ENC_ERR_OUT_OF_MEMORY",
			_ => $"NVENC_ERR_0x{value:X8}"
		};
	}

	public static string CudaResultToString(int code)
	{
		return code == 0 ? "CUDA_SUCCESS" : $"CUDA_ERR_{code}";
	}

	public static uint EncodeStructVersion<T>(uint structVersion) where T : struct
	{
		var size = (uint)Marshal.SizeOf<T>();
		return (size & 0xFFFFu) | ((structVersion & 0xFFFFu) << 16) | (0x7u << 28);
	}

	public static bool IsApiCompatible(uint maxSupportedVersion, uint requestedApiVersion)
	{
		static uint Normalize(uint v)
		{
			if (v <= 0xFFFFu)
			{
				return v;
			}

			// Some code paths represent major version as 0x000B0000 style.
			// Convert that to the compact 0xB0 style used by the runtime API query.
			var hi = (v >> 16) & 0xFFFFu;
			var lo = v & 0xFFFFu;
			if (lo == 0 && hi > 0 && hi <= 0xFF)
			{
				return hi << 4;
			}

			return v;
		}

		var max = Normalize(maxSupportedVersion);
		var req = Normalize(requestedApiVersion);
		return max >= req;
	}

	public const uint NVENCAPI_VERSION = 0x000000D0;
	public const uint NV_ENC_DEVICE_TYPE_DIRECTX = 1;
	public const uint NV_ENC_INITIALIZE_PARAMS_VER = 1;
	public const uint NV_ENC_CONFIG_VER = 1;
	public const uint NV_ENC_CREATE_BITSTREAM_BUFFER_VER = 1;
	public const uint NV_ENC_REGISTER_RESOURCE_VER = 1;
	public const uint NV_ENC_MAP_INPUT_RESOURCE_VER = 1;
	public const uint NV_ENC_PIC_PARAMS_VER = 1;
	public const uint NV_ENC_LOCK_BITSTREAM_VER = 1;
	public const uint NV_ENC_INPUT_RESOURCE_TYPE_DIRECTX = 0;
	public const uint NV_ENC_BUFFER_FORMAT_ARGB = 0;
	public const uint NV_ENC_PIC_STRUCT_FRAME = 1;

	[StructLayout(LayoutKind.Sequential)]
	public struct NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS
	{
		public uint version;
		public uint deviceType;
		public IntPtr device;
		public IntPtr reserved;
		public uint apiVersion;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct NV_ENC_INITIALIZE_PARAMS
	{
		public uint version;
		public Guid encodeGUID;
		public Guid presetGUID;
		public uint encodeWidth;
		public uint encodeHeight;
		public uint darWidth;
		public uint darHeight;
		public uint frameRateNum;
		public uint frameRateDen;
		public uint enableEncodeAsync;
		public uint enablePTD;
		public uint reportSliceOffsets;
		public uint enableSubFrameWrite;
		public uint enableExternalMEHints;
		public uint enableMEOnlyMode;
		public uint enableWeightedPrediction;
		public uint reservedBitFields;
		public uint privDataSize;
		public IntPtr privData;
		public IntPtr encodeConfig;
		public uint maxEncodeWidth;
		public uint maxEncodeHeight;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct NV_ENC_CONFIG
	{
		public uint version;
		public uint reserved0;
		public uint reserved1;
		public uint reserved2;
		public uint reserved3;
		public uint reserved4;
		public uint reserved5;
		public uint reserved6;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct NV_ENC_CREATE_BITSTREAM_BUFFER
	{
		public uint version;
		public uint size;
		public uint memoryHeap;
		public uint reserved;
		public IntPtr bitstreamBuffer;
		public IntPtr bitstreamBufferPtr;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct NV_ENC_REGISTER_RESOURCE
	{
		public uint version;
		public uint resourceType;
		public uint width;
		public uint height;
		public uint pitch;
		public uint subResourceIndex;
		public IntPtr resourceToRegister;
		public IntPtr registeredResource;
		public uint bufferFormat;
		public uint reserved;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct NV_ENC_MAP_INPUT_RESOURCE
	{
		public uint version;
		public IntPtr registeredResource;
		public IntPtr mappedResource;
		public uint mappedBufferFmt;
		public uint reserved;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct NV_ENC_PIC_PARAMS
	{
		public uint version;
		public uint inputWidth;
		public uint inputHeight;
		public uint inputPitch;
		public uint encodePicFlags;
		public uint frameIdx;
		public IntPtr inputTimeStamp;
		public IntPtr inputBuffer;
		public IntPtr outputBitstream;
		public uint completionEvent;
		public uint bufferFmt;
		public uint pictureStruct;
		public uint pictureType;
		public uint sliceMode;
		public uint sliceModeData;
		public IntPtr codecPicParams;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct NV_ENC_LOCK_BITSTREAM
	{
		public uint version;
		public uint doNotWait;
		public uint getRCStats;
		public uint reservedBitFields;
		public IntPtr outputBitstream;
		public IntPtr sliceOffsets;
		public uint frameIdx;
		public uint hwEncodeStatus;
		public IntPtr bitstreamBufferPtr;
		public uint bitstreamSizeInBytes;
		public uint bitstreamTimeStamp;
		public uint bitstreamDuration;
		public uint pictureType;
		public uint pictureStruct;
		public uint frameAvgQP;
		public uint frameSatd;
		public uint ltrFrame;
		public uint ltrFrameIdx;
	}
}
