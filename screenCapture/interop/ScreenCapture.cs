using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

internal static class GraphicsCaptureItemFactory
{
	private static readonly Guid IID_IGraphicsCaptureItem = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

	public static GraphicsCaptureItem CreateForWindow(IntPtr hwnd)
	{
		var interop = GetInterop();
		var iid = IID_IGraphicsCaptureItem;
		var hr = interop.CreateForWindow(hwnd, ref iid, out var itemPtr);
		if (hr < 0)
		{
			Marshal.ThrowExceptionForHR(hr);
		}

		try
		{
			return WinRT.MarshalInspectable<GraphicsCaptureItem>.FromAbi(itemPtr);
		}
		finally
		{
			Marshal.Release(itemPtr);
		}
	}

	public static GraphicsCaptureItem CreateForMonitor(IntPtr hmon)
	{
		var interop = GetInterop();
		var iid = IID_IGraphicsCaptureItem;
		var hr = interop.CreateForMonitor(hmon, ref iid, out var itemPtr);
		if (hr < 0)
		{
			Marshal.ThrowExceptionForHR(hr);
		}

		try
		{
			return WinRT.MarshalInspectable<GraphicsCaptureItem>.FromAbi(itemPtr);
		}
		finally
		{
			Marshal.Release(itemPtr);
		}
	}

	public static IntPtr GetMonitorHandle(int monitorIndex)
	{
		var screens = Screen.AllScreens;
		var index = (monitorIndex >= 0 && monitorIndex < screens.Length) ? monitorIndex : 0;
		var bounds = screens[index].Bounds;
		var center = new POINT
		{
			X = bounds.Left + (bounds.Width / 2),
			Y = bounds.Top + (bounds.Height / 2)
		};

		return MonitorFromPoint(center, MONITOR_DEFAULTTONEAREST);
	}

	private const int MONITOR_DEFAULTTONEAREST = 2;

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromPoint(POINT pt, int dwFlags);

	[StructLayout(LayoutKind.Sequential)]
	private struct POINT
	{
		public int X;
		public int Y;
	}

	private static IGraphicsCaptureItemInterop GetInterop()
	{
		const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
		var hr = WindowsCreateString(className, className.Length, out var hString);
		if (hr < 0)
		{
			Marshal.ThrowExceptionForHR(hr);
		}

		try
		{
			var iid = typeof(IGraphicsCaptureItemInterop).GUID;
			hr = RoGetActivationFactory(hString, ref iid, out var factory);
			if (hr < 0)
			{
				Marshal.ThrowExceptionForHR(hr);
			}

			try
			{
				return (IGraphicsCaptureItemInterop)Marshal.GetTypedObjectForIUnknown(
					factory,
					typeof(IGraphicsCaptureItemInterop));
			}
			finally
			{
				Marshal.Release(factory);
			}
		}
		finally
		{
			WindowsDeleteString(hString);
		}
	}

	[DllImport("combase.dll", ExactSpelling = true)]
	private static extern int RoGetActivationFactory(IntPtr hString, ref Guid iid, out IntPtr factory);

	[DllImport("combase.dll", ExactSpelling = true)]
	private static extern int WindowsCreateString(
		[MarshalAs(UnmanagedType.LPWStr)] string sourceString,
		int length,
		out IntPtr hString);

	[DllImport("combase.dll", ExactSpelling = true)]
	private static extern int WindowsDeleteString(IntPtr hString);
}

[ComImport]
[Guid("3628e81b-3cac-4c60-b7f4-23ce0e0c3356")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IGraphicsCaptureItemInterop
{
	[PreserveSig]
	int CreateForWindow(IntPtr window, [In] ref Guid iid, out IntPtr result);
	[PreserveSig]
	int CreateForMonitor(IntPtr monitor, [In] ref Guid iid, out IntPtr result);
}

internal static class D3D11Helper
{
	public static D3D11DeviceBundle CreateDevice()
	{
		var d3dDevice = D3D11.D3D11CreateDevice(
			Vortice.Direct3D.DriverType.Hardware,
			DeviceCreationFlags.BgraSupport,
			Vortice.Direct3D.FeatureLevel.Level_11_0);

		var dxgiDevice = d3dDevice.QueryInterface<IDXGIDevice>();
		var hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice.NativePointer, out var graphicsDevice);
		if (hr < 0 || graphicsDevice == IntPtr.Zero)
		{
			Marshal.ThrowExceptionForHR(hr);
		}

		try
		{
			var winRtDevice = WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(graphicsDevice);
			return new D3D11DeviceBundle(d3dDevice, dxgiDevice, winRtDevice);
		}
		finally
		{
			Marshal.Release(graphicsDevice);
		}
	}

	[DllImport("d3d11.dll", ExactSpelling = true)]
	private static extern int CreateDirect3D11DeviceFromDXGIDevice(
		IntPtr dxgiDevice,
		out IntPtr graphicsDevice);
}

[ComImport]
[Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDirect3DDxgiInterfaceAccess
{
	IntPtr GetInterface(ref Guid iid);
}

internal sealed class D3D11DeviceBundle : IDisposable
{
	public ID3D11Device Device { get; }
	public ID3D11DeviceContext Context { get; }
	public IDirect3DDevice WinRtDevice { get; }
	private readonly IDXGIDevice _dxgiDevice;

	public D3D11DeviceBundle(ID3D11Device device, IDXGIDevice dxgiDevice, IDirect3DDevice winRtDevice)
	{
		Device = device;
		Context = device.ImmediateContext;
		_dxgiDevice = dxgiDevice;
		WinRtDevice = winRtDevice;
	}

	public void Dispose()
	{
		WinRtDevice.Dispose();
		_dxgiDevice.Dispose();
		Context.Dispose();
		Device.Dispose();
	}
}

internal static class GdiScaler
{
	public static byte[] ScaleBgra(byte[] src, int srcWidth, int srcHeight, int dstWidth, int dstHeight)
	{
		using var srcBitmap = new System.Drawing.Bitmap(srcWidth, srcHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		var rect = new System.Drawing.Rectangle(0, 0, srcWidth, srcHeight);
		var data = srcBitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		try
		{
			Marshal.Copy(src, 0, data.Scan0, src.Length);
		}
		finally
		{
			srcBitmap.UnlockBits(data);
		}

		using var dstBitmap = new System.Drawing.Bitmap(dstWidth, dstHeight, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		using (var g = System.Drawing.Graphics.FromImage(dstBitmap))
		{
			g.DrawImage(srcBitmap, 0, 0, dstWidth, dstHeight);
		}

		var dstRect = new System.Drawing.Rectangle(0, 0, dstWidth, dstHeight);
		var dstData = dstBitmap.LockBits(dstRect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		try
		{
			var buffer = new byte[dstWidth * dstHeight * 4];
			Marshal.Copy(dstData.Scan0, buffer, 0, buffer.Length);
			return buffer;
		}
		finally
		{
			dstBitmap.UnlockBits(dstData);
		}
	}
}
