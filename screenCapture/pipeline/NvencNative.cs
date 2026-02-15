using System.Runtime.InteropServices;

internal static class NvencNative
{
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncodeApiCreateInstanceDelegate(IntPtr functionList);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	public delegate int NvEncodeApiGetMaxSupportedVersionDelegate(out uint version);
}
