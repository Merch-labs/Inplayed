using System.IO;

namespace inplayed;

internal static class FindFirstExistingDistinctPathAlgorithm
{
	public static string Run(IEnumerable<string> candidatePaths)
	{
		var checkedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		foreach (var candidatePath in candidatePaths)
		{
			if (!checkedPaths.Add(candidatePath))
			{
				continue;
			}

			if (File.Exists(candidatePath))
			{
				return candidatePath;
			}
		}

		return string.Empty;
	}
}
