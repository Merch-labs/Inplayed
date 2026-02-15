using System.Runtime.InteropServices;

internal static class NvencFunctionList
{
	// Oversized scratch buffer for upcoming function-list bootstrap work.
	// We intentionally do not dereference function pointers from this buffer yet.
	private const int FunctionListBufferBytes = 4096;

	public static IntPtr Allocate(uint maxSupportedVersion, out uint encodedVersion)
	{
		var apiStructVersion = SelectStructVersion(maxSupportedVersion);
		encodedVersion = EncodeStructVersion((uint)FunctionListBufferBytes, apiStructVersion);

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

	private static uint SelectStructVersion(uint maxSupportedVersion)
	{
		var reported = (maxSupportedVersion >> 16) & 0xFFFF;
		if (reported == 0)
		{
			return 2;
		}

		return Math.Min(reported, 2u);
	}

	private static uint EncodeStructVersion(uint structSize, uint structVersion)
	{
		return (structSize & 0xFFFFu) | ((structVersion & 0xFFFFu) << 16) | (0x7u << 28);
	}
}
