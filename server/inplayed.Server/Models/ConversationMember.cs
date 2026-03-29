namespace inplayed.Server.Models;

internal sealed class ConversationMember
{
	public Guid ConversationId { get; set; }
	public Guid UserId { get; set; }
	public DateTime JoinedAtUtc { get; set; } = DateTime.UtcNow;
	public Conversation? Conversation { get; set; }
	public SocialUser? User { get; set; }
}
