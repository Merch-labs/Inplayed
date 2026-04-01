namespace inplayed;

internal static class SocialServerSettings
{
	private const string DefaultBaseUrl = "http://localhost:5167";
	private const string EnvironmentVariableName = "INPLAYED_SOCIAL_SERVER_URL";

	public static string GetBaseUrl()
	{
		var value = Environment.GetEnvironmentVariable(EnvironmentVariableName)?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(value))
		{
			return DefaultBaseUrl;
		}

		return value.TrimEnd('/');
	}
}
