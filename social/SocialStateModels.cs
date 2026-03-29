namespace inplayed;

internal sealed class SocialState
{
	public List<SocialConversation> Conversations { get; set; } = [];
}

internal sealed class SocialConversation
{
	public string FriendName { get; set; } = string.Empty;
	public List<SocialMessage> Messages { get; set; } = [];
}

internal sealed class SocialMessage
{
	public string Id { get; set; } = Guid.NewGuid().ToString("N");
	public string Author { get; set; } = "You";
	public string Kind { get; set; } = SocialMessageKinds.Text;
	public string Body { get; set; } = string.Empty;
	public string ClipPath { get; set; } = string.Empty;
	public string ClipFileName { get; set; } = string.Empty;
	public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

internal static class SocialMessageKinds
{
	public const string Text = "text";
	public const string Clip = "clip";
}
