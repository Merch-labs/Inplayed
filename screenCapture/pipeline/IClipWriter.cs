public interface IClipWriter
{
	Task WriteAsync(string outputPath, EncodedPacketSnapshot snapshot, CancellationToken token = default);
}
