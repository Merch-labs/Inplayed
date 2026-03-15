internal static class ClipTimingEstimator
{
	public static int ResolveFps(EncodedPacketSnapshot snapshot, TimeSpan? maxDuration, int fallbackFps)
	{
		var frameCount = EstimateFrameCount(snapshot.Packets);
		if (frameCount <= 0)
		{
			return fallbackFps;
		}

		var observedSeconds = EstimateObservedDurationSeconds(snapshot.Packets);
		double? targetSeconds = null;

		if (maxDuration.HasValue && maxDuration.Value > TimeSpan.Zero)
		{
			targetSeconds = maxDuration.Value.TotalSeconds;
			if (observedSeconds.HasValue)
			{
				targetSeconds = Math.Min(targetSeconds.Value, observedSeconds.Value);
			}
		}
		else
		{
			targetSeconds = observedSeconds;
		}

		if (!targetSeconds.HasValue || targetSeconds.Value <= 0.01)
		{
			return fallbackFps;
		}

		var estimated = (int)Math.Round(frameCount / targetSeconds.Value, MidpointRounding.AwayFromZero);
		return Math.Clamp(estimated, 1, 240);
	}

	internal static int EstimateFrameCount(IReadOnlyList<EncodedPacket> packets)
	{
		var uniqueTimestamps = new HashSet<long>();
		var framesWithoutTimestamps = 0;
		for (var i = 0; i < packets.Count; i++)
		{
			var nalType = GetNalType(packets[i].Data.Span);
			if (nalType is < 1 or > 5)
			{
				continue;
			}

			var ts = packets[i].PresentationTimestamp;
			if (ts > 0)
			{
				uniqueTimestamps.Add(ts);
			}
			else
			{
				framesWithoutTimestamps++;
			}
		}

		if (uniqueTimestamps.Count > 0)
		{
			return uniqueTimestamps.Count + framesWithoutTimestamps;
		}

		return framesWithoutTimestamps;
	}

	internal static double? EstimateObservedDurationSeconds(IReadOnlyList<EncodedPacket> packets)
	{
		var timestamps = new SortedSet<long>();
		for (var i = 0; i < packets.Count; i++)
		{
			var nalType = GetNalType(packets[i].Data.Span);
			if (nalType is >= 1 and <= 5 && packets[i].PresentationTimestamp > 0)
			{
				timestamps.Add(packets[i].PresentationTimestamp);
			}
		}

		if (timestamps.Count < 2)
		{
			return null;
		}

		var first = timestamps.Min;
		var last = timestamps.Max;
		if (last <= first)
		{
			return null;
		}

		var seconds = (last - first) / 1000.0;
		return seconds > 0.01 ? seconds : null;
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
}
