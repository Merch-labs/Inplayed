using System.Text.Json;

namespace inplayed.Tests;

public sealed class SocialServiceTests : IDisposable
{
	private readonly string _tempDirectory;
	private readonly string _storePath;

	public SocialServiceTests()
	{
		_tempDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "inplayed-social-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDirectory);
		_storePath = System.IO.Path.Combine(_tempDirectory, "social.local.json");
	}

	[Fact]
	public void AddFriend_PersistsConversation()
	{
		var service = CreateService();

		var added = service.AddFriend("Alice", out var friendName);

		Assert.True(added);
		Assert.Equal("Alice", friendName);
		Assert.Contains("Alice", service.GetFriends());
		Assert.True(File.Exists(_storePath));
	}

	[Fact]
	public void SendTextMessage_SavesMessageToConversation()
	{
		var service = CreateService();
		service.AddFriend("Alice", out _);

		var message = service.SendTextMessage("Alice", "Hello there");

		Assert.NotNull(message);
		var messages = service.GetMessages("Alice");
		Assert.Single(messages);
		Assert.Equal(SocialMessageKinds.Text, messages[0].Kind);
		Assert.Equal("Hello there", messages[0].Body);
	}

	[Fact]
	public void ShareClip_SavesClipMessageWithPath()
	{
		var service = CreateService();
		service.AddFriend("Alice", out _);

		var shared = service.ShareClip("Alice", @"C:\clips\clip_001.mp4", "Clean win");

		Assert.NotNull(shared);
		var messages = service.GetMessages("Alice");
		Assert.Single(messages);
		Assert.Equal(SocialMessageKinds.Clip, messages[0].Kind);
		Assert.Equal("clip_001.mp4", messages[0].ClipFileName);
		Assert.Equal("Clean win", messages[0].Body);
	}

	[Fact]
	public void ExistingStore_LoadsPersistedFriendsAndMessages()
	{
		var state = new SocialState
		{
			Conversations =
			[
				new SocialConversation
				{
					FriendName = "Alice",
					Messages =
					[
						new SocialMessage
						{
							Author = "You",
							Kind = SocialMessageKinds.Text,
							Body = "Persisted",
							CreatedAtUtc = DateTime.UtcNow
						}
					]
				}
			]
		};

		using (var stream = File.Create(_storePath))
		{
			JsonSerializer.Serialize(stream, state);
		}

		var service = CreateService();

		Assert.Contains("Alice", service.GetFriends());
		Assert.Single(service.GetMessages("Alice"));
		Assert.Equal("Persisted", service.GetMessages("Alice")[0].Body);
	}

	private SocialService CreateService()
	{
		return new SocialService(new LocalSocialStore(_storePath));
	}

	public void Dispose()
	{
		try
		{
			if (Directory.Exists(_tempDirectory))
			{
				Directory.Delete(_tempDirectory, recursive: true);
			}
		}
		catch
		{
		}
	}
}
