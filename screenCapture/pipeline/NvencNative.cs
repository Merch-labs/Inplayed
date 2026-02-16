using System.Runtime.InteropServices;

internal static class NvencNative
{
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
