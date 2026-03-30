namespace inplayed;

internal sealed class SocialFriendRequest
{
	public int Id { get; set; }
	public string Username { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public DateTime CreatedAtUtc { get; set; }

	public override string ToString()
	{
		if (string.IsNullOrWhiteSpace(DisplayName) || string.Equals(DisplayName, Username, StringComparison.OrdinalIgnoreCase))
		{
			return Username;
		}

		return $"{DisplayName} (@{Username})";
	}
}
