public sealed class H264AnnexBPacketizer
{
	private const int MaxPendingBytes = 8 * 1024 * 1024;
	private const int KeepTailBytes = 1024 * 1024;
	private readonly List<byte> _buffer = new(1024 * 1024);
	private readonly List<int> _nalStarts = new(2048);
	private readonly List<EncodedPacket> _packets = new(512);

	public IReadOnlyList<EncodedPacket> Push(ReadOnlySpan<byte> data, long pts, long dts)
	{
		if (!data.IsEmpty)
		{
			for (var i = 0; i < data.Length; i++)
			{
				_buffer.Add(data[i]);
			}
		}

		_packets.Clear();
		FindStartCodes(_buffer, _nalStarts);
		if (_nalStarts.Count < 2)
		{
			TrimPendingBufferIfNeeded(_nalStarts);
			return Array.Empty<EncodedPacket>();
		}

		for (var i = 0; i < _nalStarts.Count - 1; i++)
		{
			var start = _nalStarts[i];
			var end = _nalStarts[i + 1];
			var size = end - start;
			if (size <= 0)
			{
				continue;
			}

			var chunk = new byte[size];
			_buffer.CopyTo(start, chunk, 0, size);
			var keyframe = IsKeyframeNal(chunk);
			_packets.Add(new EncodedPacket(chunk, pts, dts, keyframe));
		}

		var keepFrom = _nalStarts[^1];
		if (keepFrom > 0)
		{
			_buffer.RemoveRange(0, keepFrom);
		}

		if (_packets.Count == 0)
		{
			return Array.Empty<EncodedPacket>();
		}

		return _packets.ToArray();
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

	private void TrimPendingBufferIfNeeded(List<int> nalStarts)
	{
		if (_buffer.Count <= MaxPendingBytes)
		{
			return;
		}

		if (nalStarts.Count > 0)
		{
			var lastStart = nalStarts[^1];
			if (lastStart > 0 && lastStart < _buffer.Count)
			{
				_buffer.RemoveRange(0, lastStart);
			}
		}

		if (_buffer.Count > MaxPendingBytes)
		{
			var trim = _buffer.Count - KeepTailBytes;
			if (trim > 0)
			{
				_buffer.RemoveRange(0, trim);
			}
		}
	}

	private static void FindStartCodes(List<byte> bytes, List<int> starts)
	{
		starts.Clear();
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
