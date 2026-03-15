public sealed class EncodedPacketSnapshot
{
	public IReadOnlyList<EncodedPacket> Packets { get; }
	public long StartTimestampMs { get; }
	public long EndTimestampMs { get; }

	public EncodedPacketSnapshot(IReadOnlyList<EncodedPacket> packets, long startTimestampMs = 0, long endTimestampMs = 0)
	{
		Packets = packets;
		StartTimestampMs = startTimestampMs;
		EndTimestampMs = endTimestampMs;
	}
}
