namespace inplayed.Server.Models;

internal sealed class SocialPost
{
	public int Id { get; set; }
	public int AuthorId { get; set; }
	public string Caption { get; set; } = string.Empty;
	public string MediaUrl { get; set; } = string.Empty;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
