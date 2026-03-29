namespace inplayed.Server.Models;

internal sealed class SocialUser
{
	public int Id { get; set; }
	public string Username { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
