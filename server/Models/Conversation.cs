namespace inplayed.Server.Models;

internal sealed class Conversation
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public ConversationKind Kind { get; set; } = ConversationKind.Direct;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

internal enum ConversationKind
{
	Direct,
	Group
}
