public sealed class H264AnnexBPacketizer
{
	private readonly List<byte> _buffer = new(1024 * 1024);

	public IReadOnlyList<EncodedPacket> Push(ReadOnlySpan<byte> data, long pts, long dts)
	{
		if (!data.IsEmpty)
		{
			for (var i = 0; i < data.Length; i++)
			{
				_buffer.Add(data[i]);
			}
		}

		var packets = new List<EncodedPacket>();
		var nalStarts = FindStartCodes(_buffer);
		if (nalStarts.Count < 2)
		{
			return packets;
		}

		for (var i = 0; i < nalStarts.Count - 1; i++)
		{
			var start = nalStarts[i];
			var end = nalStarts[i + 1];
			var size = end - start;
			if (size <= 0)
			{
				continue;
			}

			var chunk = new byte[size];
			_buffer.CopyTo(start, chunk, 0, size);
			var keyframe = IsKeyframeNal(chunk);
			packets.Add(new EncodedPacket(chunk, pts, dts, keyframe));
		}

		var keepFrom = nalStarts[^1];
		if (keepFrom > 0)
		{
			_buffer.RemoveRange(0, keepFrom);
		}

		return packets;
	}

	public IReadOnlyList<EncodedPacket> Flush(long pts, long dts)
	{
		if (_buffer.Count == 0)
		{
			return Array.Empty<EncodedPacket>();
		}

		var all = _buffer.ToArray();
		_buffer.Clear();
		return new[]
		{
			new EncodedPacket(all, pts, dts, IsKeyframeNal(all))
		};
	}

	private static List<int> FindStartCodes(List<byte> bytes)
	{
		var starts = new List<int>();
		for (var i = 0; i < bytes.Count - 3; i++)
		{
			if (bytes[i] == 0x00 && bytes[i + 1] == 0x00)
			{
				if (bytes[i + 2] == 0x01)
				{
					starts.Add(i);
					i += 2;
					continue;
				}

				if (i + 3 < bytes.Count && bytes[i + 2] == 0x00 && bytes[i + 3] == 0x01)
				{
					starts.Add(i);
					i += 3;
				}
			}
		}

		if (starts.Count == 0 && bytes.Count > 0)
		{
			starts.Add(0);
		}

		return starts;
	}

	private static bool IsKeyframeNal(ReadOnlySpan<byte> nal)
	{
		if (nal.Length < 5)
		{
			return false;
		}

		var idx = 0;
		if (nal[0] == 0x00 && nal[1] == 0x00 && nal[2] == 0x01)
		{
			idx = 3;
		}
		else if (nal.Length >= 5 && nal[0] == 0x00 && nal[1] == 0x00 && nal[2] == 0x00 && nal[3] == 0x01)
		{
			idx = 4;
		}
		else
		{
			return false;
		}

		var nalType = nal[idx] & 0x1F;
		return nalType == 5;
	}
}
