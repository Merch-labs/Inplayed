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
		// Compare major.minor words packed by NVENC.
		var maxHi = (maxSupportedVersion >> 16) & 0xFFFFu;
		var maxLo = maxSupportedVersion & 0xFFFFu;
		var reqHi = (requestedApiVersion >> 16) & 0xFFFFu;
		var reqLo = requestedApiVersion & 0xFFFFu;
		return maxHi > reqHi || (maxHi == reqHi && maxLo >= reqLo);
	}

	public const uint NVENCAPI_VERSION = 0x000B0000;
	public const uint NV_ENC_DEVICE_TYPE_DIRECTX = 1;

	[StructLayout(LayoutKind.Sequential)]
	public struct NV_ENC_OPEN_ENCODE_SESSION_EX_PARAMS
	{
		public uint version;
		public uint deviceType;
		public IntPtr device;
		public IntPtr reserved;
		public uint apiVersion;
	}
}
