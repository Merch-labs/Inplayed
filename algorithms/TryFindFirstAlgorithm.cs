namespace inplayed;

internal static class TryFindFirstAlgorithm
{
	public static bool Run<T>(IEnumerable<T> items, Predicate<T> match, out T result)
	{
		foreach (var item in items)
		{
			if (match(item))
			{
				result = item;
				return true;
			}
		}

		result = default!;
		return false;
	}
}
