using System.Runtime.InteropServices;
using Vortice.Direct3D11;

public sealed class TextureFrameRef : IDisposable
{
	public ID3D11Texture2D Texture { get; }
	public long Timestamp { get; }
	public int Width { get; }
	public int Height { get; }

	public TextureFrameRef(ID3D11Texture2D texture, int width, int height, long timestamp)
	{
		Texture = texture;
		Width = width;
		Height = height;
		Timestamp = timestamp;
	}

	public static TextureFrameRef FromNativePtr(IntPtr texturePtr, int width, int height, long timestamp)
	{
		if (texturePtr == IntPtr.Zero)
		{
			throw new ArgumentException("Texture pointer cannot be zero.", nameof(texturePtr));
		}

		var texture = new ID3D11Texture2D(texturePtr);
		return new TextureFrameRef(texture, width, height, timestamp);
	}

	public void Dispose()
	{
		Texture.Dispose();
	}
}
