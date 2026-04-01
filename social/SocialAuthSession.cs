namespace inplayed;

internal static class SocialAuthSession
{
	public static string SessionToken { get; private set; } = string.Empty;
	public static string Username { get; private set; } = string.Empty;
	public static string DisplayName { get; private set; } = string.Empty;

	public static bool IsLoggedIn =>
		!string.IsNullOrWhiteSpace(SessionToken) &&
		!string.IsNullOrWhiteSpace(Username);

	public static void Set(string sessionToken, string username, string displayName)
	{
		SessionToken = sessionToken;
		Username = username;
		DisplayName = displayName;
	}

	public static void Clear()
	{
		SessionToken = string.Empty;
		Username = string.Empty;
		DisplayName = string.Empty;
	}
}
