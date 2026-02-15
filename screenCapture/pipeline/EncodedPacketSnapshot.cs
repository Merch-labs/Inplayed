public sealed class EncodedPacketSnapshot
{
	public IReadOnlyList<EncodedPacket> Packets { get; }

	public EncodedPacketSnapshot(IReadOnlyList<EncodedPacket> packets)
	{
		Packets = packets;
	}
}
