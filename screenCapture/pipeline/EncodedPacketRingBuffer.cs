using System.Runtime.InteropServices;

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
		var data = ToOwnedArray(packet.Data);
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
		var keep = duration <= TimeSpan.Zero ? _retention : duration;

		lock (_gate)
		{
			var latestTimestampMs = _packets.Last?.Value.TimestampMs ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			TrimLocked(latestTimestampMs, _retention);

			var minTimeMs = latestTimestampMs - (long)keep.TotalMilliseconds;
			var windowStart = _packets.First;
			while (windowStart != null && windowStart.Value.TimestampMs < minTimeMs)
			{
				windowStart = windowStart.Next;
			}

			if (windowStart == null)
			{
				return new EncodedPacketSnapshot(Array.Empty<EncodedPacket>());
			}

			var start = FindKeyframeStart(windowStart);
			if (start == null)
			{
				return new EncodedPacketSnapshot(Array.Empty<EncodedPacket>());
			}
			var list = new List<EncodedPacket>(_packets.Count);
			for (var node = start; node != null; node = node.Next)
			{
				list.Add(node.Value.Packet);
			}

			return new EncodedPacketSnapshot(list);
		}
	}

	private LinkedListNode<BufferedPacket>? FindKeyframeStart(LinkedListNode<BufferedPacket> windowStart)
	{
		LinkedListNode<BufferedPacket>? start = null;

		for (var node = windowStart; node != null; node = node.Previous)
		{
			if (node.Value.Packet.IsKeyFrame)
			{
				start = node;
				break;
			}
		}

		if (start == null)
		{
			for (var node = windowStart; node != null; node = node.Next)
			{
				if (node.Value.Packet.IsKeyFrame)
				{
					start = node;
					break;
				}
			}
		}

		if (start == null)
		{
			return null;
		}

		for (var node = start.Previous; node != null; node = node.Previous)
		{
			var nalType = GetNalType(node.Value.Packet.Data.Span);
			if (nalType == 7 || nalType == 8 || nalType == 6 || nalType == 9)
			{
				start = node;
				continue;
			}

			break;
		}

		return start;
	}

	public (int packetCount, long totalBytes) GetStats()
	{
		lock (_gate)
		{
			return (_packets.Count, _bytes);
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

	private static byte[] ToOwnedArray(ReadOnlyMemory<byte> data)
	{
		if (MemoryMarshal.TryGetArray(data, out ArraySegment<byte> segment))
		{
			if (segment.Array != null)
			{
				if (segment.Offset == 0 && segment.Count == segment.Array.Length)
				{
					return segment.Array;
				}

				var copy = new byte[segment.Count];
				Buffer.BlockCopy(segment.Array, segment.Offset, copy, 0, segment.Count);
				return copy;
			}
		}

		return data.ToArray();
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
