using Vortice.DXGI;

internal static class GpuCapabilityProbe
{
	private const int NvidiaVendorId = 0x10DE;

	public static bool IsNvidiaAdapterPresent()
	{
		try
		{
			return ContainsVendorId(EnumerateAdapterVendorIds(), NvidiaVendorId);
		}
		catch
		{
			return false;
		}
	}

	internal static bool ContainsVendorId(IEnumerable<int> vendorIds, int vendorId)
	{
		foreach (var currentVendorId in vendorIds)
		{
			if (currentVendorId == vendorId)
			{
				return true;
			}
		}

		return false;
	}

	private static IEnumerable<int> EnumerateAdapterVendorIds()
	{
		using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
		if (factory == null)
		{
			yield break;
		}

		for (uint index = 0; ; index++)
		{
			var result = factory.EnumAdapters1(index, out var adapter);
			if (result.Failure || adapter == null)
			{
				yield break;
			}

			using (adapter)
			{
				yield return unchecked((int)adapter.Description1.VendorId);
			}
		}
	}
}
