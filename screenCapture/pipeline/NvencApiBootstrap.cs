using System.Runtime.InteropServices;

internal static class NvencApiBootstrap
{
	public static bool TryReadOpenSessionPointer(
		IntPtr functionListBuffer,
		out IntPtr openSessionPtr,
		out string message)
	{
		openSessionPtr = IntPtr.Zero;
		message = "unknown";
		if (functionListBuffer == IntPtr.Zero)
		{
			message = "function list buffer is zero";
			return false;
		}

		var pointerSize = IntPtr.Size;
		var offset = sizeof(uint); // skip version field
		openSessionPtr = pointerSize == 8
			? new IntPtr(Marshal.ReadInt64(functionListBuffer, offset))
			: new IntPtr(Marshal.ReadInt32(functionListBuffer, offset));

		if (openSessionPtr == IntPtr.Zero)
		{
			message = "nvEncOpenEncodeSessionEx pointer not populated";
			return false;
		}

		message = "ok";
		return true;
	}

	public static bool TryBindOpenSessionDelegate(
		IntPtr openSessionPtr,
		out NvencNative.NvEncOpenEncodeSessionExDelegate? openSessionDelegate,
		out string message)
	{
		openSessionDelegate = null;
		message = "unknown";
		if (openSessionPtr == IntPtr.Zero)
		{
			message = "openSession pointer is zero";
			return false;
		}

		try
		{
			openSessionDelegate = Marshal.GetDelegateForFunctionPointer<NvencNative.NvEncOpenEncodeSessionExDelegate>(
				openSessionPtr);
			message = "ok";
			return true;
		}
		catch (Exception ex)
		{
			message = $"bind_failed:{ex.GetType().Name}";
			return false;
		}
	}
}
