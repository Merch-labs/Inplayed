public interface IEncodedPacketBuffer
{
	void Append(EncodedPacket packet);
	EncodedPacketSnapshot SnapshotLast(TimeSpan duration);
}
