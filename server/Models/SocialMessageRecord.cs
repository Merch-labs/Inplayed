namespace inplayed.Server.Models;

internal sealed class SocialMessageRecord
{
	public int Id { get; set; }
	public int ConversationId { get; set; }
	public int SenderId { get; set; }
	public string Body { get; set; } = string.Empty;
	public string MediaUrl { get; set; } = string.Empty;
	public string MediaType { get; set; } = string.Empty;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
