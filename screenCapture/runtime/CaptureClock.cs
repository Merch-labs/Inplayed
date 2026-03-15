using System.Diagnostics;

internal static class CaptureClock
{
	public static long NowMilliseconds()
	{
		return ToMilliseconds(Stopwatch.GetTimestamp());
	}

	internal static long ToMilliseconds(long timestamp)
	{
		if (timestamp <= 0)
		{
			return 0;
		}

		return (long)Math.Round(timestamp * 1000.0 / Stopwatch.Frequency);
	}
}
