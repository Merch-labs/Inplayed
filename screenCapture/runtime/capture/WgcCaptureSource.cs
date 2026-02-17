using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;

public sealed class WgcCaptureSource : ICaptureSource
{
	private static readonly Guid _d3d11Texture2DIid = typeof(Vortice.Direct3D11.ID3D11Texture2D).GUID;
	private static readonly object _getInterfaceGate = new();
	private static GetInterfaceDelegate? _getInterface;

	private readonly GraphicsCaptureItem _item;
	private readonly D3D11DeviceBundle _device;
	private Direct3D11CaptureFramePool? _framePool;
	private GraphicsCaptureSession? _session;
	private Windows.Graphics.SizeInt32 _lastSize;
	private bool _running;

	public event Action<TextureFrameRef>? FrameArrived;
	public event Action<int, int>? ResolutionChanged;

	public WgcCaptureSource(GraphicsCaptureItem item)
	{
		_item = item;
		_device = D3D11Helper.CreateDevice();
		_lastSize = item.Size;
	}

	public Task StartAsync(CancellationToken token)
	{
		if (_running)
		{
			return Task.CompletedTask;
		}

		_framePool = Direct3D11CaptureFramePool.Create(
			_device.WinRtDevice,
			DirectXPixelFormat.B8G8R8A8UIntNormalized,
			2,
			_lastSize);
		_framePool.FrameArrived += OnFrameArrived;

		_session = _framePool.CreateCaptureSession(_item);
		_session.StartCapture();
		_running = true;
		return Task.CompletedTask;
	}

	public Task StopAsync()
	{
		if (!_running)
		{
			return Task.CompletedTask;
		}

		_running = false;
		if (_framePool != null)
		{
			_framePool.FrameArrived -= OnFrameArrived;
		}

		_session?.Dispose();
		_session = null;
		_framePool?.Dispose();
		_framePool = null;
		return Task.CompletedTask;
	}

	private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
	{
		using var frame = sender.TryGetNextFrame();
		if (frame == null)
		{
			return;
		}

		var size = frame.ContentSize;
		if (size.Width != _lastSize.Width || size.Height != _lastSize.Height)
		{
			_lastSize = size;
			sender.Recreate(
				_device.WinRtDevice,
				DirectXPixelFormat.B8G8R8A8UIntNormalized,
				2,
				_lastSize);
			ResolutionChanged?.Invoke(size.Width, size.Height);
		}

		var handler = FrameArrived;
		if (handler == null)
		{
			return;
		}

		var surfaceRef = WinRT.MarshalInterface<IDirect3DSurface>.CreateMarshaler2(frame.Surface);
		var surfaceAbi = WinRT.MarshalInterface<IDirect3DSurface>.GetAbi(surfaceRef);
		try
		{
			var accessIid = typeof(IDirect3DDxgiInterfaceAccess).GUID;
			var hr = Marshal.QueryInterface(surfaceAbi, in accessIid, out var accessPtr);
			if (hr < 0)
			{
				Marshal.ThrowExceptionForHR(hr);
			}

			IntPtr texturePtr;
			try
			{
				texturePtr = GetTextureInterface(accessPtr);
			}
			finally
			{
				Marshal.Release(accessPtr);
			}

			var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			var textureFrame = TextureFrameRef.FromNativePtr(texturePtr, size.Width, size.Height, ts);
			handler(textureFrame);
		}
		finally
		{
			WinRT.MarshalInterface<IDirect3DSurface>.DisposeMarshaler(surfaceRef);
		}
	}

	public void Dispose()
	{
		StopAsync().GetAwaiter().GetResult();
		_device.Dispose();
	}

	private static IntPtr GetTextureInterface(IntPtr accessPtr)
	{
		var del = _getInterface;
		if (del == null)
		{
			lock (_getInterfaceGate)
			{
				del = _getInterface;
				if (del == null)
				{
					var vtbl = Marshal.ReadIntPtr(accessPtr);
					var fnPtr = Marshal.ReadIntPtr(vtbl, IntPtr.Size * 3);
					del = Marshal.GetDelegateForFunctionPointer<GetInterfaceDelegate>(fnPtr);
					_getInterface = del;
				}
			}
		}

		var iid = _d3d11Texture2DIid;
		var hr = del(accessPtr, ref iid, out var texturePtr);
		if (hr < 0)
		{
			Marshal.ThrowExceptionForHR(hr);
		}

		return texturePtr;
	}

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int GetInterfaceDelegate(IntPtr @this, ref Guid iid, out IntPtr result);
}
