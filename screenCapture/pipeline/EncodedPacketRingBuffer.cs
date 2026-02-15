public sealed class EncodedPacketRingBuffer : IEncodedPacketBuffer
{
	private readonly object _gate = new();
	private readonly LinkedList<BufferedPacket> _packets = new();
	private readonly TimeSpan _retention;
	private long _bytes;
	private readonly long _maxBytes;

	public EncodedPacketRingBuffer(TimeSpan retention, long maxBytes = 256L * 1024L * 1024L)
	{
		_retention = retention <= TimeSpan.Zero ? TimeSpan.FromSeconds(60) : retention;
		_maxBytes = Math.Max(8L * 1024L * 1024L, maxBytes);
	}

	public void Append(EncodedPacket packet)
	{
		var data = packet.Data.ToArray();
		var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var packetTimeMs = packet.PresentationTimestamp > 0 ? packet.PresentationTimestamp : nowMs;
		var node = new BufferedPacket(
			new EncodedPacket(data, packet.PresentationTimestamp, packet.DecodeTimestamp, packet.IsKeyFrame),
			packetTimeMs);

		lock (_gate)
		{
			_packets.AddLast(node);
			_bytes += data.Length;
			TrimLocked(packetTimeMs, _retention);
			TrimBytesLocked();
		}
	}

	public EncodedPacketSnapshot SnapshotLast(TimeSpan duration)
	{
		var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
		var keep = duration <= TimeSpan.Zero ? _retention : duration;

		lock (_gate)
		{
			TrimLocked(nowMs, _retention);

			var minTimeMs = nowMs - (long)keep.TotalMilliseconds;
			var list = new List<EncodedPacket>(_packets.Count);
			for (var node = _packets.First; node != null; node = node.Next)
			{
				if (node.Value.TimestampMs >= minTimeMs)
				{
					list.Add(node.Value.Packet);
				}
			}

			var keyframeIndex = 0;
			for (var i = 0; i < list.Count; i++)
			{
				if (list[i].IsKeyFrame)
				{
					keyframeIndex = i;
					break;
				}
			}

			if (keyframeIndex > 0)
			{
				var start = keyframeIndex;
				for (var i = keyframeIndex - 1; i >= 0; i--)
				{
					var nalType = GetNalType(list[i].Data.Span);
					if (nalType == 7 || nalType == 8 || nalType == 6 || nalType == 9)
					{
						start = i;
						continue;
					}

					break;
				}

				list = list.Skip(start).ToList();
			}

			return new EncodedPacketSnapshot(list);
		}
	}

	private static int GetNalType(ReadOnlySpan<byte> data)
	{
		if (data.Length < 5)
		{
			return -1;
		}

		var idx = 0;
		if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x01)
		{
			idx = 3;
		}
		else if (data[0] == 0x00 && data[1] == 0x00 && data[2] == 0x00 && data[3] == 0x01)
		{
			idx = 4;
		}
		else
		{
			return -1;
		}

		return data[idx] & 0x1F;
	}

	private void TrimLocked(long nowMs, TimeSpan keep)
	{
		var cutoffMs = nowMs - (long)keep.TotalMilliseconds;
		while (_packets.First != null && _packets.First.Value.TimestampMs < cutoffMs)
		{
			var removed = _packets.First.Value;
			_bytes -= removed.Packet.Data.Length;
			_packets.RemoveFirst();
		}
	}

	private void TrimBytesLocked()
	{
		while (_bytes > _maxBytes && _packets.First != null)
		{
			var removed = _packets.First.Value;
			_bytes -= removed.Packet.Data.Length;
			_packets.RemoveFirst();
		}
	}

	private readonly record struct BufferedPacket(EncodedPacket Packet, long TimestampMs);
}
