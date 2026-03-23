namespace inplayed;

internal sealed class SocialService
{
	private readonly LocalSocialStore _store;
	private readonly SocialState _state;

	public SocialService(LocalSocialStore? store = null)
	{
		_store = store ?? new LocalSocialStore();
		_state = _store.Load();
		NormalizeState();
	}

	public IReadOnlyList<string> GetFriends()
	{
		return _state.Conversations
			.Select(conversation => conversation.FriendName)
			.Where(name => !string.IsNullOrWhiteSpace(name))
			.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public IReadOnlyList<SocialMessage> GetMessages(string friendName)
	{
		var conversation = FindConversation(friendName);
		if (conversation == null)
		{
			return [];
		}

		return conversation.Messages
			.OrderBy(message => message.CreatedAtUtc)
			.ToList();
	}

	public bool AddFriend(string rawName, out string normalizedName)
	{
		normalizedName = NormalizeName(rawName);
		if (string.IsNullOrWhiteSpace(normalizedName) || FindConversation(normalizedName) != null)
		{
			return false;
		}

		_state.Conversations.Add(new SocialConversation
		{
			FriendName = normalizedName
		});
		Persist();
		return true;
	}

	public bool RemoveFriend(string friendName)
	{
		var conversation = FindConversation(friendName);
		if (conversation == null)
		{
			return false;
		}

		_state.Conversations.Remove(conversation);
		Persist();
		return true;
	}

	public SocialMessage? SendTextMessage(string friendName, string rawMessage)
	{
		var messageBody = rawMessage.Trim();
		if (string.IsNullOrWhiteSpace(messageBody))
		{
			return null;
		}

		var conversation = FindOrCreateConversation(friendName);
		if (conversation == null)
		{
			return null;
		}

		var message = new SocialMessage
		{
			Author = "You",
			Kind = SocialMessageKinds.Text,
			Body = messageBody,
			CreatedAtUtc = DateTime.UtcNow
		};

		conversation.Messages.Add(message);
		Persist();
		return message;
	}

	public SocialMessage? ShareClip(string friendName, string clipPath, string caption)
	{
		if (string.IsNullOrWhiteSpace(clipPath))
		{
			return null;
		}

		var conversation = FindOrCreateConversation(friendName);
		if (conversation == null)
		{
			return null;
		}

		var message = new SocialMessage
		{
			Author = "You",
			Kind = SocialMessageKinds.Clip,
			Body = string.IsNullOrWhiteSpace(caption) ? "Shared a clip" : caption.Trim(),
			ClipPath = clipPath,
			ClipFileName = System.IO.Path.GetFileName(clipPath),
			CreatedAtUtc = DateTime.UtcNow
		};

		conversation.Messages.Add(message);
		Persist();
		return message;
	}

	private void NormalizeState()
	{
		var uniqueConversations = new Dictionary<string, SocialConversation>(StringComparer.OrdinalIgnoreCase);
		foreach (var conversation in _state.Conversations)
		{
			var normalizedName = NormalizeName(conversation.FriendName);
			if (string.IsNullOrWhiteSpace(normalizedName))
			{
				continue;
			}

			if (!uniqueConversations.TryGetValue(normalizedName, out var target))
			{
				target = new SocialConversation
				{
					FriendName = normalizedName
				};
				uniqueConversations.Add(normalizedName, target);
			}

			foreach (var message in conversation.Messages.OrderBy(message => message.CreatedAtUtc))
			{
				target.Messages.Add(NormalizeMessage(message));
			}
		}

		_state.Conversations.Clear();
		_state.Conversations.AddRange(uniqueConversations.Values.OrderBy(item => item.FriendName, StringComparer.OrdinalIgnoreCase));
		Persist();
	}

	private SocialConversation? FindConversation(string rawName)
	{
		var normalizedName = NormalizeName(rawName);
		return _state.Conversations.FirstOrDefault(conversation =>
			string.Equals(conversation.FriendName, normalizedName, StringComparison.OrdinalIgnoreCase));
	}

	private SocialConversation? FindOrCreateConversation(string rawName)
	{
		var normalizedName = NormalizeName(rawName);
		if (string.IsNullOrWhiteSpace(normalizedName))
		{
			return null;
		}

		var conversation = FindConversation(normalizedName);
		if (conversation != null)
		{
			return conversation;
		}

		conversation = new SocialConversation
		{
			FriendName = normalizedName
		};
		_state.Conversations.Add(conversation);
		return conversation;
	}

	private static SocialMessage NormalizeMessage(SocialMessage? message)
	{
		if (message == null)
		{
			return new SocialMessage();
		}

		return new SocialMessage
		{
			Id = string.IsNullOrWhiteSpace(message.Id) ? Guid.NewGuid().ToString("N") : message.Id,
			Author = string.IsNullOrWhiteSpace(message.Author) ? "You" : message.Author.Trim(),
			Kind = string.IsNullOrWhiteSpace(message.Kind) ? SocialMessageKinds.Text : message.Kind,
			Body = message.Body?.Trim() ?? string.Empty,
			ClipPath = message.ClipPath ?? string.Empty,
			ClipFileName = string.IsNullOrWhiteSpace(message.ClipFileName) && !string.IsNullOrWhiteSpace(message.ClipPath)
				? System.IO.Path.GetFileName(message.ClipPath)
				: message.ClipFileName ?? string.Empty,
			CreatedAtUtc = message.CreatedAtUtc == default ? DateTime.UtcNow : message.CreatedAtUtc
		};
	}

	private void Persist()
	{
		_store.Save(_state);
	}

	private static string NormalizeName(string? rawName)
	{
		return rawName?.Trim() ?? string.Empty;
	}
}
