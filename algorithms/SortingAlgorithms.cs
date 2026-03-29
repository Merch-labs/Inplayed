namespace inplayed;

internal static class SortingAlgorithms
{
	public static List<T> InsertionSort<T>(IEnumerable<T> items, Comparison<T> comparison)
	{
		var sortedItems = new List<T>();

		foreach (var item in items)
		{
			sortedItems.Add(item);

			var currentIndex = sortedItems.Count - 1;
			while (currentIndex > 0 && comparison(sortedItems[currentIndex - 1], sortedItems[currentIndex]) > 0)
			{
				var temp = sortedItems[currentIndex - 1];
				sortedItems[currentIndex - 1] = sortedItems[currentIndex];
				sortedItems[currentIndex] = temp;
				currentIndex--;
			}
		}

		return sortedItems;
	}
}
