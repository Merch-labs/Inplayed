using Vortice.Direct3D11;
using Vortice.DXGI;

internal static class GpuCapabilityProbe
{
	private const int NvidiaVendorId = 0x10DE;

	public static bool IsNvidiaAdapterPresent()
	{
		try
		{
			using var device = D3D11.D3D11CreateDevice(
				Vortice.Direct3D.DriverType.Hardware,
				DeviceCreationFlags.BgraSupport,
				Vortice.Direct3D.FeatureLevel.Level_11_0);
			using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
			using var adapter = dxgiDevice.GetAdapter();
			var desc = adapter.Description;
			return desc.VendorId == NvidiaVendorId;
		}
		catch
		{
			return false;
		}
	}
}
