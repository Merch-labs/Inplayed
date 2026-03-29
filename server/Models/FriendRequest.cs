namespace inplayed.Server.Models;

internal sealed class FriendRequest
{
	public int Id { get; set; }
	public int RequesterId { get; set; }
	public int RecipientId { get; set; }
	public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
	public DateTime? RespondedAtUtc { get; set; }
}

internal enum FriendRequestStatus
{
	Pending,
	Accepted,
	Rejected,
	Blocked
}
