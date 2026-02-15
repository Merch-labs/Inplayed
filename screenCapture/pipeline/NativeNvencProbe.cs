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

	public static bool TryBindCreateInstance(
		IntPtr libraryHandle,
		out NvencNative.NvEncodeApiCreateInstanceDelegate? createInstance,
		out string message)
	{
		createInstance = null;
		message = "unknown";
		if (libraryHandle == IntPtr.Zero)
		{
			message = "library handle is zero";
			return false;
		}

		if (!NativeLibrary.TryGetExport(libraryHandle, "NvEncodeAPICreateInstance", out var proc))
		{
			message = "missing NvEncodeAPICreateInstance export";
			return false;
		}

		try
		{
			createInstance = Marshal.GetDelegateForFunctionPointer<NvencNative.NvEncodeApiCreateInstanceDelegate>(proc);
			message = "ok";
			return true;
		}
		catch (Exception ex)
		{
			message = $"bind_failed:{ex.GetType().Name}";
			return false;
		}
	}

	public static bool TryBindGetMaxSupportedVersion(
		IntPtr libraryHandle,
		out NvencNative.NvEncodeApiGetMaxSupportedVersionDelegate? getMaxVersion,
		out string message)
	{
		getMaxVersion = null;
		message = "unknown";
		if (libraryHandle == IntPtr.Zero)
		{
			message = "library handle is zero";
			return false;
		}

		if (!NativeLibrary.TryGetExport(libraryHandle, "NvEncodeAPIGetMaxSupportedVersion", out var proc))
		{
			message = "missing NvEncodeAPIGetMaxSupportedVersion export";
			return false;
		}

		try
		{
			getMaxVersion = Marshal.GetDelegateForFunctionPointer<NvencNative.NvEncodeApiGetMaxSupportedVersionDelegate>(proc);
			message = "ok";
			return true;
		}
		catch (Exception ex)
		{
			message = $"bind_failed:{ex.GetType().Name}";
			return false;
		}
	}

	public static bool TryLoadCuda(out IntPtr cudaHandle, out string message)
	{
		cudaHandle = IntPtr.Zero;
		message = "unknown";
		if (NativeLibrary.TryLoad("nvcuda.dll", out cudaHandle))
		{
			message = "loaded:nvcuda.dll";
			return true;
		}

		message = "nvcuda.dll not found";
		return false;
	}

	public static bool TryBindCudaInit(
		IntPtr cudaHandle,
		out NvencNative.CuInitDelegate? cuInit,
		out string message)
	{
		cuInit = null;
		message = "unknown";
		if (cudaHandle == IntPtr.Zero)
		{
			message = "cuda handle is zero";
			return false;
		}

		if (!NativeLibrary.TryGetExport(cudaHandle, "cuInit", out var proc))
		{
			message = "missing cuInit export";
			return false;
		}

		try
		{
			cuInit = Marshal.GetDelegateForFunctionPointer<NvencNative.CuInitDelegate>(proc);
			message = "ok";
			return true;
		}
		catch (Exception ex)
		{
			message = $"bind_failed:{ex.GetType().Name}";
			return false;
		}
	}

	public static bool TryBindCudaDriverGetVersion(
		IntPtr cudaHandle,
		out NvencNative.CuDriverGetVersionDelegate? cuDriverGetVersion,
		out string message)
	{
		cuDriverGetVersion = null;
		message = "unknown";
		if (cudaHandle == IntPtr.Zero)
		{
			message = "cuda handle is zero";
			return false;
		}

		if (!NativeLibrary.TryGetExport(cudaHandle, "cuDriverGetVersion", out var proc))
		{
			message = "missing cuDriverGetVersion export";
			return false;
		}

		try
		{
			cuDriverGetVersion = Marshal.GetDelegateForFunctionPointer<NvencNative.CuDriverGetVersionDelegate>(proc);
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
