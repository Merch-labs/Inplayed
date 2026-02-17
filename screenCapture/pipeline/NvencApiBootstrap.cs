using System.Runtime.InteropServices;

internal static class NvencApiBootstrap
{
	private static bool IsLikelyCodePointer(IntPtr ptr)
	{
		var value = ptr.ToInt64();
		return value > 0x10000;
	}

	public static bool TryBindDelegate<T>(IntPtr ptr, out T? del, out string message) where T : Delegate
	{
		del = null;
		message = "unknown";
		if (ptr == IntPtr.Zero)
		{
			message = "function pointer is zero";
			return false;
		}
		if (!IsLikelyCodePointer(ptr))
		{
			message = $"function pointer is invalid:0x{ptr.ToInt64():X}";
			return false;
		}

		try
		{
			del = Marshal.GetDelegateForFunctionPointer<T>(ptr);
			message = "ok";
			return true;
		}
		catch (Exception ex)
		{
			message = $"bind_failed:{ex.GetType().Name}";
			return false;
		}
	}

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
		var offset = sizeof(uint) * 2; // skip version + reserved
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
		if (!IsLikelyCodePointer(openSessionPtr))
		{
			message = $"openSession pointer invalid:0x{openSessionPtr.ToInt64():X}";
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

	public static bool TryBindGetEncodeGuidCountDelegate(
		IntPtr getEncodeGuidCountPtr,
		out NvencNative.NvEncGetEncodeGuidCountDelegate? del,
		out string message)
	{
		del = null;
		message = "unknown";
		if (getEncodeGuidCountPtr == IntPtr.Zero)
		{
			message = "getEncodeGuidCount pointer is zero";
			return false;
		}

		try
		{
			del = Marshal.GetDelegateForFunctionPointer<NvencNative.NvEncGetEncodeGuidCountDelegate>(getEncodeGuidCountPtr);
			message = "ok";
			return true;
		}
		catch (Exception ex)
		{
			message = $"bind_failed:{ex.GetType().Name}";
			return false;
		}
	}

	public static bool TryBindGetEncodeProfileGuidCountDelegate(
		IntPtr getEncodeProfileGuidCountPtr,
		out NvencNative.NvEncGetEncodeProfileGuidCountDelegate? del,
		out string message)
	{
		del = null;
		message = "unknown";
		if (getEncodeProfileGuidCountPtr == IntPtr.Zero)
		{
			message = "getEncodeProfileGuidCount pointer is zero";
			return false;
		}

		try
		{
			del = Marshal.GetDelegateForFunctionPointer<NvencNative.NvEncGetEncodeProfileGuidCountDelegate>(getEncodeProfileGuidCountPtr);
			message = "ok";
			return true;
		}
		catch (Exception ex)
		{
			message = $"bind_failed:{ex.GetType().Name}";
			return false;
		}
	}

	public static bool TryBindGetEncodeProfileGuidsDelegate(
		IntPtr ptr,
		out NvencNative.NvEncGetEncodeProfileGUIDsDelegate? del,
		out string message)
	{
		del = null;
		message = "unknown";
		if (ptr == IntPtr.Zero)
		{
			message = "getEncodeProfileGUIDs pointer is zero";
			return false;
		}

		try
		{
			del = Marshal.GetDelegateForFunctionPointer<NvencNative.NvEncGetEncodeProfileGUIDsDelegate>(ptr);
			message = "ok";
			return true;
		}
		catch (Exception ex)
		{
			message = $"bind_failed:{ex.GetType().Name}";
			return false;
		}
	}

	public static bool TryBindGetEncodeGuidsDelegate(
		IntPtr ptr,
		out NvencNative.NvEncGetEncodeGUIDsDelegate? del,
		out string message)
	{
		del = null;
		message = "unknown";
		if (ptr == IntPtr.Zero)
		{
			message = "getEncodeGUIDs pointer is zero";
			return false;
		}

		try
		{
			del = Marshal.GetDelegateForFunctionPointer<NvencNative.NvEncGetEncodeGUIDsDelegate>(ptr);
			message = "ok";
			return true;
		}
		catch (Exception ex)
		{
			message = $"bind_failed:{ex.GetType().Name}";
			return false;
		}
	}

	public static bool TryBindInitializeEncoderDelegate(
		IntPtr ptr,
		out NvencNative.NvEncInitializeEncoderDelegate? del,
		out string message)
	{
		del = null;
		message = "unknown";
		if (ptr == IntPtr.Zero)
		{
			message = "initializeEncoder pointer is zero";
			return false;
		}

		try
		{
			del = Marshal.GetDelegateForFunctionPointer<NvencNative.NvEncInitializeEncoderDelegate>(ptr);
			message = "ok";
			return true;
		}
		catch (Exception ex)
		{
			message = $"bind_failed:{ex.GetType().Name}";
			return false;
		}
	}

	public static bool TryBindGetEncodePresetCountDelegate(
		IntPtr ptr,
		out NvencNative.NvEncGetEncodePresetCountDelegate? del,
		out string message)
	{
		del = null;
		message = "unknown";
		if (ptr == IntPtr.Zero)
		{
			message = "getEncodePresetCount pointer is zero";
			return false;
		}

		try
		{
			del = Marshal.GetDelegateForFunctionPointer<NvencNative.NvEncGetEncodePresetCountDelegate>(ptr);
			message = "ok";
			return true;
		}
		catch (Exception ex)
		{
			message = $"bind_failed:{ex.GetType().Name}";
			return false;
		}
	}

	public static bool TryBindGetEncodePresetGuidsDelegate(
		IntPtr ptr,
		out NvencNative.NvEncGetEncodePresetGUIDsDelegate? del,
		out string message)
	{
		del = null;
		message = "unknown";
		if (ptr == IntPtr.Zero)
		{
			message = "getEncodePresetGUIDs pointer is zero";
			return false;
		}

		try
		{
			del = Marshal.GetDelegateForFunctionPointer<NvencNative.NvEncGetEncodePresetGUIDsDelegate>(ptr);
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
