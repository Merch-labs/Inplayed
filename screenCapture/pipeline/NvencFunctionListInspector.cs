using System.Runtime.InteropServices;

internal static class NvencFunctionListInspector
{
	public static IntPtr ReadPointerAtSlot(IntPtr functionListBuffer, int slot)
	{
		if (functionListBuffer == IntPtr.Zero || slot < 0)
		{
			return IntPtr.Zero;
		}

		var pointerSize = IntPtr.Size;
		var offset = sizeof(uint) + (slot * pointerSize);
		return pointerSize == 8
			? new IntPtr(Marshal.ReadInt64(functionListBuffer, offset))
			: new IntPtr(Marshal.ReadInt32(functionListBuffer, offset));
	}
}
