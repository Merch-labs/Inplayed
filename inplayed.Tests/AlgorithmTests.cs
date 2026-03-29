namespace inplayed.Tests;

public sealed class AlgorithmTests : IDisposable
{
	private readonly string _tempDirectory;

	public AlgorithmTests()
	{
		_tempDirectory = Path.Combine(Path.GetTempPath(), "inplayed-algorithm-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDirectory);
	}

	[Fact]
	public void InsertionSort_SortsStringsIgnoringCase()
	{
		var items = new List<string> { "charlie", "Alice", "bob" };

		var sorted = SortingAlgorithms.InsertionSort(
			items,
			(left, right) => StringComparer.OrdinalIgnoreCase.Compare(left, right));

		Assert.Equal(["Alice", "bob", "charlie"], sorted);
	}

	[Fact]
	public void TryFindFirst_ReturnsMatchingValue()
	{
		var items = new List<string> { "alice", "bob", "charlie" };

		var found = SearchAlgorithms.TryFindFirst(
			items,
			item => string.Equals(item, "BOB", StringComparison.OrdinalIgnoreCase),
			out var result);

		Assert.True(found);
		Assert.Equal("bob", result);
	}

	[Fact]
	public void FindIndex_ReturnsMatchingIndex()
	{
		System.Collections.IList items = new List<object?> { "F1", "F2", "F3" };

		var index = SearchAlgorithms.FindIndex(
			items,
			item => string.Equals(item as string, "f2", StringComparison.OrdinalIgnoreCase));

		Assert.Equal(1, index);
	}

	[Fact]
	public void FindFirstExistingDistinctPath_ReturnsFirstExistingPath()
	{
		var firstPath = Path.Combine(_tempDirectory, "first.txt");
		var secondPath = Path.Combine(_tempDirectory, "second.txt");
		File.WriteAllText(secondPath, "test");

		var resolvedPath = PathAlgorithms.FindFirstExistingDistinctPath(
			[
				firstPath,
				secondPath,
				secondPath
			]);

		Assert.Equal(secondPath, resolvedPath);
	}

	public void Dispose()
	{
		try
		{
			if (Directory.Exists(_tempDirectory))
			{
				Directory.Delete(_tempDirectory, recursive: true);
			}
		}
		catch
		{
		}
	}
}
