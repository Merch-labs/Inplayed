namespace inplayed;

internal sealed class SocialService
{
	private readonly LocalSocialStore? _store;
	private readonly SocialDatabaseStore? _databaseStore;
	private readonly SocialState? _state;

	public SocialService(LocalSocialStore? store = null)
	{
		if (store != null)
		{
			_store = store;
			_state = _store.Load();
			NormalizeState();
			return;
		}

		var config = AppConfig.Load();
		var connectionString = config.Social.DatabaseConnectionString?.Trim() ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(connectionString))
		{
			var username = config.Social.Username?.Trim();
			if (string.IsNullOrWhiteSpace(username))
			{
				username = Environment.UserName;
			}

			try
			{
				_databaseStore = new SocialDatabaseStore(connectionString, username);
				return;
			}
			catch
			{
			}
		}

		_store = new LocalSocialStore();
		_state = _store.Load();
		NormalizeState();
	}

	public IReadOnlyList<string> GetFriends()
	{
		if (_databaseStore != null)
		{
			return _databaseStore.GetFriends();
		}

		var friends = new List<string>();

		foreach (var conversation in _state!.Conversations)
		{
			if (!string.IsNullOrWhiteSpace(conversation.FriendName))
			{
				friends.Add(conversation.FriendName);
			}
		}

		return InsertionSortAlgorithm.Run(
			friends,
			(left, right) => StringComparer.OrdinalIgnoreCase.Compare(left, right));
	}

	public IReadOnlyList<SocialMessage> GetMessages(string friendName)
	{
		if (_databaseStore != null)
		{
			return _databaseStore.GetMessages(friendName);
		}

		var conversation = FindConversation(friendName);
		if (conversation == null)
		{
			return [];
		}

		var messages = new List<SocialMessage>();
		foreach (var message in conversation.Messages)
		{
			messages.Add(message);
		}

		return InsertionSortAlgorithm.Run(
			messages,
			(left, right) => left.CreatedAtUtc.CompareTo(right.CreatedAtUtc));
	}

	public bool AddFriend(string rawName, out string normalizedName)
	{
		if (_databaseStore != null)
		{
			return _databaseStore.AddFriend(rawName, out normalizedName);
		}

		normalizedName = NormalizeName(rawName);
		if (string.IsNullOrWhiteSpace(normalizedName) || FindConversation(normalizedName) != null)
		{
			return false;
		}

		_state!.Conversations.Add(new SocialConversation
		{
			FriendName = normalizedName
		});
		Persist();
		return true;
	}

	public bool RemoveFriend(string friendName)
	{
		if (_databaseStore != null)
		{
			return _databaseStore.RemoveFriend(friendName);
		}

		var conversation = FindConversation(friendName);
		if (conversation == null)
		{
			return false;
		}

		_state!.Conversations.Remove(conversation);
		Persist();
		return true;
	}

	public SocialMessage? SendTextMessage(string friendName, string rawMessage)
	{
		if (_databaseStore != null)
		{
			return _databaseStore.SendTextMessage(friendName, rawMessage);
		}

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
		if (_databaseStore != null)
		{
			return _databaseStore.ShareClip(friendName, clipPath, caption);
		}

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

		foreach (var conversation in _state!.Conversations)
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

			var orderedMessages = new List<SocialMessage>();
			foreach (var message in conversation.Messages)
			{
				orderedMessages.Add(message);
			}

			orderedMessages = InsertionSortAlgorithm.Run(
				orderedMessages,
				(left, right) => left.CreatedAtUtc.CompareTo(right.CreatedAtUtc));
			foreach (var message in orderedMessages)
			{
				target.Messages.Add(NormalizeMessage(message));
			}
		}

		_state.Conversations.Clear();
		var sortedConversations = new List<SocialConversation>();
		foreach (var conversation in uniqueConversations.Values)
		{
			sortedConversations.Add(conversation);
		}

		sortedConversations = InsertionSortAlgorithm.Run(
			sortedConversations,
			(left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.FriendName, right.FriendName));

		foreach (var conversation in sortedConversations)
		{
			_state.Conversations.Add(conversation);
		}

		Persist();
	}

	private SocialConversation? FindConversation(string rawName)
	{
		var normalizedName = NormalizeName(rawName);
		if (TryFindFirstAlgorithm.Run(
			_state!.Conversations,
			conversation => string.Equals(conversation.FriendName, normalizedName, StringComparison.OrdinalIgnoreCase),
			out var conversation))
		{
			return conversation;
		}

		return null;
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
		_state!.Conversations.Add(conversation);
		return conversation;
	}

	private static SocialMessage NormalizeMessage(SocialMessage? message)
	{
		if (message == null)
		{
			return new SocialMessage();
		}

		var clipFileName = message.ClipFileName ?? string.Empty;
		if (string.IsNullOrWhiteSpace(clipFileName) && !string.IsNullOrWhiteSpace(message.ClipPath))
		{
			clipFileName = System.IO.Path.GetFileName(message.ClipPath);
		}

		return new SocialMessage
		{
			Id = string.IsNullOrWhiteSpace(message.Id) ? Guid.NewGuid().ToString("N") : message.Id,
			Author = string.IsNullOrWhiteSpace(message.Author) ? "You" : message.Author.Trim(),
			Kind = string.IsNullOrWhiteSpace(message.Kind) ? SocialMessageKinds.Text : message.Kind,
			Body = message.Body?.Trim() ?? string.Empty,
			ClipPath = message.ClipPath ?? string.Empty,
			ClipFileName = clipFileName,
			CreatedAtUtc = message.CreatedAtUtc == default ? DateTime.UtcNow : message.CreatedAtUtc
		};
	}

	private void Persist()
	{
		if (_store == null || _state == null)
		{
			return;
		}

		_store.Save(_state);
	}

	private static string NormalizeName(string? rawName)
	{
		return rawName?.Trim() ?? string.Empty;
	}
}
