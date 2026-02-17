using System.Runtime.InteropServices;

internal static class NvencFunctionList
{
	// Oversized scratch buffer for upcoming function-list bootstrap work.
	// We intentionally do not dereference function pointers from this buffer yet.
	private const int FunctionListBufferBytes = 4096;

	public static IntPtr Allocate(uint maxSupportedVersion, out uint encodedVersion)
	{
		_ = maxSupportedVersion;
		encodedVersion = EncodeFunctionListVersion();

		var ptr = Marshal.AllocHGlobal(FunctionListBufferBytes);
		for (var i = 0; i < FunctionListBufferBytes; i++)
		{
			Marshal.WriteByte(ptr, i, 0);
		}

		Marshal.WriteInt32(ptr, unchecked((int)encodedVersion));
		return ptr;
	}

	public static void Free(ref IntPtr ptr)
	{
		if (ptr == IntPtr.Zero)
		{
			return;
		}

		Marshal.FreeHGlobal(ptr);
		ptr = IntPtr.Zero;
	}

	public static int CountNonZeroPointerSlots(IntPtr functionListPtr, int maxSlots = 64)
	{
		if (functionListPtr == IntPtr.Zero || maxSlots <= 0)
		{
			return 0;
		}

		var headerBytes = sizeof(uint) * 2;
		var pointerSize = IntPtr.Size;
		var count = 0;
		for (var i = 0; i < maxSlots; i++)
		{
			var offset = headerBytes + (i * pointerSize);
			if (offset + pointerSize > FunctionListBufferBytes)
			{
				break;
			}

			IntPtr value = pointerSize == 8
				? new IntPtr(Marshal.ReadInt64(functionListPtr, offset))
				: new IntPtr(Marshal.ReadInt32(functionListPtr, offset));
			if (value != IntPtr.Zero)
			{
				count++;
			}
		}

		return count;
	}

	private static uint EncodeFunctionListVersion()
	{
		const uint functionListStructVersion = 2;
		return functionListStructVersion | (NvencNative.NVENCAPI_VERSION << 16) | (0x7u << 28);
	}
}
