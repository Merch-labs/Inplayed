using System.Collections;

namespace inplayed;

internal static class SearchAlgorithms
{
	public static int FindIndex(IList items, Predicate<object?> match)
	{
		for (var i = 0; i < items.Count; i++)
		{
			if (match(items[i]))
			{
				return i;
			}
		}

		return -1;
	}

	public static bool Contains<T>(IEnumerable<T> items, Predicate<T> match)
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

	public static bool TryFindFirst<T>(IEnumerable<T> items, Predicate<T> match, out T result)
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
