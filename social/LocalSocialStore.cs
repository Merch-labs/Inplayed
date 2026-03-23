using System.IO;
using System.Text.Json;

namespace inplayed;

internal sealed class LocalSocialStore
{
	private static readonly JsonSerializerOptions SerializerOptions = new()
	{
		WriteIndented = true
	};

	private readonly string _path;

	public LocalSocialStore(string? path = null)
	{
		_path = string.IsNullOrWhiteSpace(path) ? ResolvePath() : path;
	}

	public SocialState Load()
	{
		try
		{
			if (!File.Exists(_path))
			{
				return new SocialState();
			}

			using var stream = File.OpenRead(_path);
			return JsonSerializer.Deserialize<SocialState>(stream, SerializerOptions) ?? new SocialState();
		}
		catch
		{
			return new SocialState();
		}
	}

	public void Save(SocialState state)
	{
		try
		{
			var directory = System.IO.Path.GetDirectoryName(_path);
			if (!string.IsNullOrWhiteSpace(directory))
			{
				Directory.CreateDirectory(directory);
			}

			using var stream = File.Create(_path);
			JsonSerializer.Serialize(stream, state, SerializerOptions);
		}
		catch
		{
		}
	}

	public static string ResolvePath()
	{
		var appDataFolder = System.IO.Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"inplayed");

		return System.IO.Path.Combine(appDataFolder, "social.local.json");
	}
}

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
