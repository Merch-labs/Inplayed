namespace inplayed;

internal sealed class SocialMessage
{
	public int Id { get; set; }
	public string Author { get; set; } = string.Empty;
	public string Kind { get; set; } = SocialMessageKinds.Text;
	public string Body { get; set; } = string.Empty;
	public string ClipPath { get; set; } = string.Empty;
	public string ClipFileName { get; set; } = string.Empty;
	public DateTime CreatedAtUtc { get; set; }
}

internal static class SocialMessageKinds
{
	public const string Text = "text";
	public const string Clip = "clip";
}
