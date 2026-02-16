using System.Runtime.InteropServices;

internal static class NvencFunctionListInspector
{
	public readonly record struct RequiredSlotsState(
		bool HasOpenSession,
		bool HasGetEncodeGuidCount,
		bool HasGetEncodeProfileGuidCount,
		bool HasGetEncodeGuids)
	{
		public bool AllPresent => HasOpenSession && HasGetEncodeGuidCount && HasGetEncodeProfileGuidCount && HasGetEncodeGuids;
	}

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

	public static RequiredSlotsState ReadRequiredSlots(IntPtr functionListBuffer)
	{
		var openSession = ReadPointerAtSlot(functionListBuffer, 0) != IntPtr.Zero;
		var getEncodeGuidCount = ReadPointerAtSlot(functionListBuffer, 1) != IntPtr.Zero;
		var getEncodeProfileGuidCount = ReadPointerAtSlot(functionListBuffer, 2) != IntPtr.Zero;
		var getEncodeGuids = ReadPointerAtSlot(functionListBuffer, 3) != IntPtr.Zero;
		return new RequiredSlotsState(openSession, getEncodeGuidCount, getEncodeProfileGuidCount, getEncodeGuids);
	}
}
