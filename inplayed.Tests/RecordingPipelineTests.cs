using System.Linq;

public sealed class RecordingPipelineTests
{
	[Fact]
	public void Packetizer_EmitsCompletedNals_AndKeepsTailUntilCompleted()
	{
		var packetizer = new H264AnnexBPacketizer();
		var idr = new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0xAA };
		var nonIdr = new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0xBB };

		var firstChunk = idr.Concat(nonIdr.Take(3)).ToArray();
		var firstPackets = packetizer.Push(firstChunk, pts: 1000, dts: 1000);

		Assert.Empty(firstPackets);

		var secondPackets = packetizer.Push(nonIdr.Skip(3).ToArray(), pts: 1033, dts: 1033);

		Assert.Single(secondPackets);
		Assert.True(secondPackets[0].IsKeyFrame);
		Assert.Equal(idr, secondPackets[0].Data.ToArray());

		var flushed = packetizer.Flush(1033, 1033);
		Assert.Single(flushed);
		Assert.False(flushed[0].IsKeyFrame);
		Assert.Equal(nonIdr, flushed[0].Data.ToArray());
	}

	[Fact]
	public void RingBuffer_SnapshotLast_StartsAtNearestDecodableKeyframeBoundary()
	{
		var ring = new EncodedPacketRingBuffer(TimeSpan.FromSeconds(10));

		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x67, 0x10 }, 1000, 1000, false));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x68, 0x20 }, 1000, 1000, false));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0x30 }, 1000, 1000, true));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x40 }, 1500, 1500, false));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x50 }, 2000, 2000, false));

		var snapshot = ring.SnapshotLast(TimeSpan.FromMilliseconds(700));

		Assert.Equal(5, snapshot.Packets.Count);
		Assert.Equal(7, GetNalType(snapshot.Packets[0].Data.Span));
		Assert.Equal(8, GetNalType(snapshot.Packets[1].Data.Span));
		Assert.True(snapshot.Packets[2].IsKeyFrame);
	}

	[Fact]
	public void RingBuffer_TrimsOldPacketsOutsideRetentionWindow()
	{
		var ring = new EncodedPacketRingBuffer(TimeSpan.FromMilliseconds(500));

		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0x01 }, 1000, 1000, true));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x41, 0x02 }, 1200, 1200, false));
		ring.Append(new EncodedPacket(new byte[] { 0x00, 0x00, 0x00, 0x01, 0x65, 0x03 }, 2000, 2000, true));

		var snapshot = ring.SnapshotLast(TimeSpan.FromMilliseconds(300));

		Assert.Single(snapshot.Packets);
		Assert.True(snapshot.Packets[0].IsKeyFrame);
		Assert.Equal(0x03, snapshot.Packets[0].Data.Span[^1]);
	}

	private static int GetNalType(ReadOnlySpan<byte> data)
	{
		if (data.Length < 5)
		{
			return -1;
		}

		var index = data[2] == 0x01 ? 3 : 4;
		return data[index] & 0x1F;
	}
}
