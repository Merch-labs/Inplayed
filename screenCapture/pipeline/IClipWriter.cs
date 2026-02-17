public interface IClipWriter
{
	Task WriteAsync(string outputPath, EncodedPacketSnapshot snapshot, TimeSpan? maxDuration = null, CancellationToken token = default);
}
