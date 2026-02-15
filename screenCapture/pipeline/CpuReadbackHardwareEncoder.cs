using System.Threading.Tasks;
using Vortice.Direct3D11;

public sealed class CpuReadbackHardwareEncoder : IHardwareEncoder
{
	private readonly string _videoCodec;
	private readonly object _gate = new();
	private RecordingSettings? _settings;
	private FfmpegEncoder? _encoder;
	private D3D11DeviceBundle? _device;
	private ID3D11Texture2D? _staging;
	private int _stagingWidth;
	private int _stagingHeight;
	private int _outputWidth;
	private int _outputHeight;

	public CpuReadbackHardwareEncoder(string videoCodec = "libx264")
	{
		_videoCodec = string.IsNullOrWhiteSpace(videoCodec) ? "libx264" : videoCodec;
	}

	public void Start(RecordingSettings settings)
	{
		lock (_gate)
		{
			_settings = settings;
			_outputWidth = settings.Width;
			_outputHeight = settings.Height;
			_encoder = new FfmpegEncoder(settings, _videoCodec);
			_device = D3D11Helper.CreateDevice();
		}
	}

	public void Encode(TextureFrameRef frame)
	{
		lock (_gate)
		{
			if (_settings == null || _encoder == null || _device == null)
			{
				return;
			}

			EnsureStaging(frame.Width, frame.Height, frame.Texture);
			if (_staging == null)
			{
				return;
			}

			_device.Context.CopyResource(_staging, frame.Texture);
			var dataBox = _device.Context.Map(_staging, 0, MapMode.Read, MapFlags.None);
			try
			{
				var width = frame.Width;
				var height = frame.Height;
				var rowBytes = width * 4;
				var buffer = new byte[rowBytes * height];
				for (var y = 0; y < height; y++)
				{
					var src = IntPtr.Add(dataBox.DataPointer, (int)(y * dataBox.RowPitch));
					System.Runtime.InteropServices.Marshal.Copy(src, buffer, y * rowBytes, rowBytes);
				}

				if (width != _outputWidth || height != _outputHeight)
				{
					buffer = GdiScaler.ScaleBgra(buffer, width, height, _outputWidth, _outputHeight);
					width = _outputWidth;
					height = _outputHeight;
				}

				_encoder.PushFrame(new VideoFrame(buffer, width, height, frame.Timestamp));
			}
			finally
			{
				_device.Context.Unmap(_staging, 0);
			}
		}
	}

	public void Reconfigure(int width, int height)
	{
		lock (_gate)
		{
			_outputWidth = Math.Max(1, width);
			_outputHeight = Math.Max(1, height);
		}
	}

	public void Stop()
	{
	}

	public Task FlushRecentAsync(string path, TimeSpan clipLength, CancellationToken token = default)
	{
		lock (_gate)
		{
			if (_encoder == null)
			{
				return Task.CompletedTask;
			}

			return _encoder.FlushRecentAsync(path, clipLength);
		}
	}

	public void Dispose()
	{
		lock (_gate)
		{
			_staging?.Dispose();
			_staging = null;
			_device?.Dispose();
			_device = null;
			_encoder?.Dispose();
			_encoder = null;
		}
	}

	private void EnsureStaging(int width, int height, ID3D11Texture2D sourceTexture)
	{
		if (_staging != null && width == _stagingWidth && height == _stagingHeight)
		{
			return;
		}

		_staging?.Dispose();
		var desc = sourceTexture.Description;
		desc.Usage = ResourceUsage.Staging;
		desc.BindFlags = BindFlags.None;
		desc.CPUAccessFlags = CpuAccessFlags.Read;
		desc.MiscFlags = ResourceOptionFlags.None;
		desc.MipLevels = 1;
		desc.ArraySize = 1;

		_staging = _device!.Device.CreateTexture2D(desc);
		_stagingWidth = width;
		_stagingHeight = height;
	}
}
