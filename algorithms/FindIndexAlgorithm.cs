using System.Collections;

namespace inplayed;

internal static class FindIndexAlgorithm
{
	public static int Run(IList items, Predicate<object?> match)
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
}
