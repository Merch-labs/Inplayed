using System.Runtime.InteropServices;

internal static class NativeNvencProbe
{
	public static bool TryLoad(out IntPtr libraryHandle, out string message)
	{
		libraryHandle = IntPtr.Zero;
		message = "unknown";

		var candidates = new[]
		{
			"nvEncodeAPI64.dll",
			"nvEncodeAPI.dll"
		};

		for (var i = 0; i < candidates.Length; i++)
		{
			if (NativeLibrary.TryLoad(candidates[i], out libraryHandle))
			{
				message = $"loaded:{candidates[i]}";
				return true;
			}
		}

		message = "nvEncodeAPI runtime not found";
		return false;
	}

	public static bool HasCreateInstanceExport(IntPtr libraryHandle, out string message)
	{
		message = "unknown";
		if (libraryHandle == IntPtr.Zero)
		{
			message = "library handle is zero";
			return false;
		}

		if (!NativeLibrary.TryGetExport(libraryHandle, "NvEncodeAPICreateInstance", out _))
		{
			message = "missing NvEncodeAPICreateInstance export";
			return false;
		}

		message = "ok";
		return true;
	}
}
