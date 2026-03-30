namespace inplayed;

internal static class SocialAuthSession
{
	public static int UserId { get; private set; }
	public static string Username { get; private set; } = string.Empty;
	public static string DisplayName { get; private set; } = string.Empty;

	public static bool IsLoggedIn => UserId > 0 && !string.IsNullOrWhiteSpace(Username);

	public static void Set(int userId, string username, string displayName)
	{
		UserId = userId;
		Username = username;
		DisplayName = displayName;
	}

	public static void Clear()
	{
		UserId = 0;
		Username = string.Empty;
		DisplayName = string.Empty;
	}
}
