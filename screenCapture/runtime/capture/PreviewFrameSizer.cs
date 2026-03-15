public static class PreviewFrameSizer
{
	public const int DefaultMaxDimension = 960;

	public static (int Width, int Height) GetScaledSize(int sourceWidth, int sourceHeight, int maxDimension = DefaultMaxDimension)
	{
		if (sourceWidth <= 0 || sourceHeight <= 0)
		{
			return (1, 1);
		}

		if (maxDimension <= 0)
		{
			return (sourceWidth, sourceHeight);
		}

		var longestEdge = Math.Max(sourceWidth, sourceHeight);
		if (longestEdge <= maxDimension)
		{
			return (sourceWidth, sourceHeight);
		}

		var scale = (double)maxDimension / longestEdge;
		var width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
		var height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
		return (width, height);
	}
}
