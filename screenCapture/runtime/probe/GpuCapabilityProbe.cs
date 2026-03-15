using Vortice.DXGI;

internal static class GpuCapabilityProbe
{
	private const int NvidiaVendorId = 0x10DE;
	private static readonly object _cacheGate = new();
	private static bool? _hasNvidiaAdapter;

	public static bool IsNvidiaAdapterPresent()
	{
		var cached = _hasNvidiaAdapter;
		if (cached.HasValue)
		{
			return cached.Value;
		}

		try
		{
			var hasNvidiaAdapter = ContainsVendorId(EnumerateAdapterVendorIds(), NvidiaVendorId);
			lock (_cacheGate)
			{
				_hasNvidiaAdapter ??= hasNvidiaAdapter;
				return _hasNvidiaAdapter.Value;
			}
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
