namespace inplayed.Server.Models;

internal sealed class SocialPost
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid AuthorId { get; set; }
	public string Caption { get; set; } = string.Empty;
	public string MediaUrl { get; set; } = string.Empty;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
