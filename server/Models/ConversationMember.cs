namespace inplayed.Server.Models;

internal sealed class ConversationMember
{
	public int ConversationId { get; set; }
	public int UserId { get; set; }
	public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
}
