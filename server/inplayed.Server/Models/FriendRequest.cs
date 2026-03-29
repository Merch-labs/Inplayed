namespace inplayed.Server.Models;

internal sealed class FriendRequest
{
	public Guid Id { get; set; } = Guid.NewGuid();
	public Guid RequesterId { get; set; }
	public Guid RecipientId { get; set; }
	public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
	public DateTime? RespondedAtUtc { get; set; }
	public SocialUser? Requester { get; set; }
	public SocialUser? Recipient { get; set; }
}

internal enum FriendRequestStatus
{
	Pending,
	Accepted,
	Rejected,
	Blocked
}
