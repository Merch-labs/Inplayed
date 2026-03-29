namespace inplayed;

internal static class ContainsAlgorithm
{
	public static bool Run<T>(IEnumerable<T> items, Predicate<T> match)
	{
		foreach (var item in items)
		{
			if (match(item))
			{
				return true;
			}
		}

		return false;
	}
}
