using System.IO;
using Npgsql;

namespace inplayed;

internal sealed class SocialDatabaseStore
{
	private readonly string _connectionString;
	private int _currentUserId;

	public static bool TryConnect(string connectionString, string username, out string message)
	{
		message = string.Empty;

		var trimmedConnectionString = connectionString?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(trimmedConnectionString))
		{
			message = "Enter a database connection string first.";
			return false;
		}

		var trimmedUsername = username?.Trim();
		if (string.IsNullOrWhiteSpace(trimmedUsername))
		{
			trimmedUsername = Environment.UserName;
		}

		try
		{
			_ = new SocialDatabaseStore(trimmedConnectionString, trimmedUsername);
			message = "Connected to the social database.";
			return true;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return false;
		}
	}

	public SocialDatabaseStore(string connectionString, string username)
	{
		_connectionString = connectionString;
		var currentDisplayName = NormalizeName(username);
		var currentUsername = BuildUsername(currentDisplayName);
		EnsureCreated();
		_currentUserId = EnsureUser(currentUsername, currentDisplayName);
	}

	public IReadOnlyList<string> GetFriends()
	{
		var friends = new List<string>();

		using var connection = CreateOpenConnection();
		using var command = new NpgsqlCommand(
			@"SELECT u.display_name
			  FROM friend_requests fr
			  JOIN users u
			    ON u.id =
			        CASE
			            WHEN fr.requester_id = @userId THEN fr.recipient_id
			            ELSE fr.requester_id
			        END
			  WHERE fr.status = 'Accepted'
			    AND (fr.requester_id = @userId OR fr.recipient_id = @userId)
			  ORDER BY u.display_name",
			connection);
		command.Parameters.AddWithValue("userId", _currentUserId);

		using var reader = command.ExecuteReader();
		while (reader.Read())
		{
			friends.Add(reader.GetString(0));
		}

		return friends;
	}

	public IReadOnlyList<SocialMessage> GetMessages(string friendName)
	{
		var friendUserId = FindUserId(friendName);
		if (friendUserId <= 0)
		{
			return [];
		}

		var conversationId = FindDirectConversationId(friendUserId);
		if (conversationId <= 0)
		{
			return [];
		}

		var messages = new List<SocialMessage>();

		using var connection = CreateOpenConnection();
		using var command = new NpgsqlCommand(
			@"SELECT m.id, m.sender_id, u.display_name, m.body, m.media_url, m.media_type, m.created_at_utc
			  FROM messages m
			  JOIN users u ON u.id = m.sender_id
			  WHERE m.conversation_id = @conversationId
			  ORDER BY m.created_at_utc",
			connection);
		command.Parameters.AddWithValue("conversationId", conversationId);

		using var reader = command.ExecuteReader();
		while (reader.Read())
		{
			var senderId = reader.GetInt32(1);
			var mediaUrl = reader.GetString(4);
			var mediaType = reader.GetString(5);
			var isClip = string.Equals(mediaType, SocialMessageKinds.Clip, StringComparison.OrdinalIgnoreCase);

			messages.Add(new SocialMessage
			{
				Id = reader.GetInt32(0).ToString(),
				Author = senderId == _currentUserId ? "You" : reader.GetString(2),
				Kind = isClip ? SocialMessageKinds.Clip : SocialMessageKinds.Text,
				Body = reader.GetString(3),
				ClipPath = isClip ? mediaUrl : string.Empty,
				ClipFileName = isClip ? Path.GetFileName(mediaUrl) : string.Empty,
				CreatedAtUtc = reader.GetDateTime(6)
			});
		}

		return messages;
	}

	public bool AddFriend(string rawName, out string normalizedName)
	{
		normalizedName = NormalizeName(rawName);
		if (string.IsNullOrWhiteSpace(normalizedName))
		{
			return false;
		}

		var friendUsername = BuildUsername(normalizedName);
		var friendId = EnsureUser(friendUsername, normalizedName);
		if (friendId == _currentUserId)
		{
			return false;
		}

		using var connection = CreateOpenConnection();
		using var existingCommand = new NpgsqlCommand(
			@"SELECT COUNT(*) FROM friend_requests
			  WHERE (requester_id = @currentUserId AND recipient_id = @friendId)
			     OR (requester_id = @friendId AND recipient_id = @currentUserId)",
			connection);
		existingCommand.Parameters.AddWithValue("currentUserId", _currentUserId);
		existingCommand.Parameters.AddWithValue("friendId", friendId);

		var count = Convert.ToInt32(existingCommand.ExecuteScalar());
		if (count > 0)
		{
			return false;
		}

		using var command = new NpgsqlCommand(
			@"INSERT INTO friend_requests
			  (requester_id, recipient_id, status, created_at_utc, responded_at_utc)
			  VALUES (@currentUserId, @friendId, @status, @createdAtUtc, @respondedAtUtc)",
			connection);
		command.Parameters.AddWithValue("currentUserId", _currentUserId);
		command.Parameters.AddWithValue("friendId", friendId);
		command.Parameters.AddWithValue("status", "Accepted");
		command.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);
		command.Parameters.AddWithValue("respondedAtUtc", DateTime.UtcNow);
		command.ExecuteNonQuery();
		return true;
	}

	public bool RemoveFriend(string friendName)
	{
		var friendId = FindUserId(friendName);
		if (friendId <= 0)
		{
			return false;
		}

		var conversationId = FindDirectConversationId(friendId);

		using var connection = CreateOpenConnection();

		if (conversationId > 0)
		{
			using var deleteMessages = new NpgsqlCommand("DELETE FROM messages WHERE conversation_id = @conversationId", connection);
			deleteMessages.Parameters.AddWithValue("conversationId", conversationId);
			deleteMessages.ExecuteNonQuery();

			using var deleteMembers = new NpgsqlCommand("DELETE FROM conversation_members WHERE conversation_id = @conversationId", connection);
			deleteMembers.Parameters.AddWithValue("conversationId", conversationId);
			deleteMembers.ExecuteNonQuery();

			using var deleteConversation = new NpgsqlCommand("DELETE FROM conversations WHERE id = @conversationId", connection);
			deleteConversation.Parameters.AddWithValue("conversationId", conversationId);
			deleteConversation.ExecuteNonQuery();
		}

		using var deleteFriendship = new NpgsqlCommand(
			@"DELETE FROM friend_requests
			  WHERE (requester_id = @currentUserId AND recipient_id = @friendId)
			     OR (requester_id = @friendId AND recipient_id = @currentUserId)",
			connection);
		deleteFriendship.Parameters.AddWithValue("currentUserId", _currentUserId);
		deleteFriendship.Parameters.AddWithValue("friendId", friendId);

		return deleteFriendship.ExecuteNonQuery() > 0;
	}

	public SocialMessage? SendTextMessage(string friendName, string rawMessage)
	{
		var messageBody = rawMessage.Trim();
		if (string.IsNullOrWhiteSpace(messageBody))
		{
			return null;
		}

		var friendId = EnsureFriend(friendName);
		if (friendId <= 0)
		{
			return null;
		}

		var conversationId = EnsureConversation(friendId);
		var createdAtUtc = DateTime.UtcNow;

		using var connection = CreateOpenConnection();
		using var command = new NpgsqlCommand(
			@"INSERT INTO messages
			  (conversation_id, sender_id, body, media_url, media_type, created_at_utc)
			  VALUES (@conversationId, @senderId, @body, @mediaUrl, @mediaType, @createdAtUtc)
			  RETURNING id",
			connection);
		command.Parameters.AddWithValue("conversationId", conversationId);
		command.Parameters.AddWithValue("senderId", _currentUserId);
		command.Parameters.AddWithValue("body", messageBody);
		command.Parameters.AddWithValue("mediaUrl", string.Empty);
		command.Parameters.AddWithValue("mediaType", SocialMessageKinds.Text);
		command.Parameters.AddWithValue("createdAtUtc", createdAtUtc);

		var id = Convert.ToInt32(command.ExecuteScalar());
		return new SocialMessage
		{
			Id = id.ToString(),
			Author = "You",
			Kind = SocialMessageKinds.Text,
			Body = messageBody,
			CreatedAtUtc = createdAtUtc
		};
	}

	public SocialMessage? ShareClip(string friendName, string clipPath, string caption)
	{
		if (string.IsNullOrWhiteSpace(clipPath))
		{
			return null;
		}

		var friendId = EnsureFriend(friendName);
		if (friendId <= 0)
		{
			return null;
		}

		var conversationId = EnsureConversation(friendId);
		var createdAtUtc = DateTime.UtcNow;
		var body = string.IsNullOrWhiteSpace(caption) ? "Shared a clip" : caption.Trim();

		using var connection = CreateOpenConnection();
		using var command = new NpgsqlCommand(
			@"INSERT INTO messages
			  (conversation_id, sender_id, body, media_url, media_type, created_at_utc)
			  VALUES (@conversationId, @senderId, @body, @mediaUrl, @mediaType, @createdAtUtc)
			  RETURNING id",
			connection);
		command.Parameters.AddWithValue("conversationId", conversationId);
		command.Parameters.AddWithValue("senderId", _currentUserId);
		command.Parameters.AddWithValue("body", body);
		command.Parameters.AddWithValue("mediaUrl", clipPath);
		command.Parameters.AddWithValue("mediaType", SocialMessageKinds.Clip);
		command.Parameters.AddWithValue("createdAtUtc", createdAtUtc);

		var id = Convert.ToInt32(command.ExecuteScalar());
		return new SocialMessage
		{
			Id = id.ToString(),
			Author = "You",
			Kind = SocialMessageKinds.Clip,
			Body = body,
			ClipPath = clipPath,
			ClipFileName = Path.GetFileName(clipPath),
			CreatedAtUtc = createdAtUtc
		};
	}

	private void EnsureCreated()
	{
		using var connection = CreateOpenConnection();
		using var command = new NpgsqlCommand(
			"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users'",
			connection);
		var count = Convert.ToInt32(command.ExecuteScalar());
		if (count > 0)
		{
			return;
		}

		var schemaPath = ResolveSchemaPath();
		if (string.IsNullOrWhiteSpace(schemaPath) || !File.Exists(schemaPath))
		{
			throw new FileNotFoundException("Could not find social database schema file.", schemaPath);
		}

		var sql = File.ReadAllText(schemaPath);
		using var createCommand = new NpgsqlCommand(sql, connection);
		createCommand.ExecuteNonQuery();
	}

	private NpgsqlConnection CreateOpenConnection()
	{
		var connection = new NpgsqlConnection(_connectionString);
		connection.Open();
		return connection;
	}

	private int EnsureFriend(string friendName)
	{
		var normalizedName = NormalizeName(friendName);
		if (string.IsNullOrWhiteSpace(normalizedName))
		{
			return 0;
		}

		var friendUsername = BuildUsername(normalizedName);
		var friendId = EnsureUser(friendUsername, normalizedName);
		if (friendId == _currentUserId)
		{
			return 0;
		}

		return friendId;
	}

	private int EnsureUser(string username, string displayName)
	{
		using var connection = CreateOpenConnection();
		using var findCommand = new NpgsqlCommand(
			"SELECT id FROM users WHERE LOWER(username) = LOWER(@username)",
			connection);
		findCommand.Parameters.AddWithValue("username", username);

		var result = findCommand.ExecuteScalar();
		if (result != null && result != DBNull.Value)
		{
			return Convert.ToInt32(result);
		}

		using var insertCommand = new NpgsqlCommand(
			@"INSERT INTO users (username, display_name, created_at_utc)
			  VALUES (@username, @displayName, @createdAtUtc)
			  RETURNING id",
			connection);
		insertCommand.Parameters.AddWithValue("username", username);
		insertCommand.Parameters.AddWithValue("displayName", displayName);
		insertCommand.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);
		return Convert.ToInt32(insertCommand.ExecuteScalar());
	}

	private int FindUserId(string friendName)
	{
		var normalizedName = NormalizeName(friendName);
		if (string.IsNullOrWhiteSpace(normalizedName))
		{
			return 0;
		}

		using var connection = CreateOpenConnection();
		using var command = new NpgsqlCommand(
			@"SELECT id
			  FROM users
			  WHERE LOWER(username) = LOWER(@username)
			     OR LOWER(display_name) = LOWER(@displayName)",
			connection);
		command.Parameters.AddWithValue("username", BuildUsername(normalizedName));
		command.Parameters.AddWithValue("displayName", normalizedName);

		var result = command.ExecuteScalar();
		if (result == null || result == DBNull.Value)
		{
			return 0;
		}

		return Convert.ToInt32(result);
	}

	private int FindDirectConversationId(int friendId)
	{
		using var connection = CreateOpenConnection();
		using var command = new NpgsqlCommand(
			@"SELECT c.id
			  FROM conversations c
			  JOIN conversation_members cm ON cm.conversation_id = c.id
			  WHERE c.kind = 'Direct'
			    AND cm.user_id IN (@currentUserId, @friendId)
			  GROUP BY c.id
			  HAVING COUNT(*) = 2",
			connection);
		command.Parameters.AddWithValue("currentUserId", _currentUserId);
		command.Parameters.AddWithValue("friendId", friendId);

		var result = command.ExecuteScalar();
		if (result == null || result == DBNull.Value)
		{
			return 0;
		}

		return Convert.ToInt32(result);
	}

	private int EnsureConversation(int friendId)
	{
		var existingId = FindDirectConversationId(friendId);
		if (existingId > 0)
		{
			return existingId;
		}

		using var connection = CreateOpenConnection();
		using var insertConversation = new NpgsqlCommand(
			@"INSERT INTO conversations (kind, created_at_utc)
			  VALUES (@kind, @createdAtUtc)
			  RETURNING id",
			connection);
		insertConversation.Parameters.AddWithValue("kind", "Direct");
		insertConversation.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);
		var conversationId = Convert.ToInt32(insertConversation.ExecuteScalar());

		var memberIds = new[] { _currentUserId, friendId };
		foreach (var memberId in memberIds)
		{
			using var insertMember = new NpgsqlCommand(
				@"INSERT INTO conversation_members (conversation_id, user_id, joined_at_utc)
				  VALUES (@conversationId, @userId, @joinedAtUtc)",
				connection);
			insertMember.Parameters.AddWithValue("conversationId", conversationId);
			insertMember.Parameters.AddWithValue("userId", memberId);
			insertMember.Parameters.AddWithValue("joinedAtUtc", DateTime.UtcNow);
			insertMember.ExecuteNonQuery();
		}

		return conversationId;
	}

	private static string NormalizeName(string? value)
	{
		return value?.Trim() ?? string.Empty;
	}

	private static string BuildUsername(string displayName)
	{
		return NormalizeName(displayName).ToLowerInvariant();
	}

	private static string ResolveSchemaPath()
	{
		var candidates = new List<string>
		{
			Path.Combine(AppContext.BaseDirectory, "server", "sql", "schema.sql"),
			Path.Combine(Environment.CurrentDirectory, "server", "sql", "schema.sql"),
			Path.Combine(AppContext.BaseDirectory, "sql", "schema.sql"),
			Path.Combine(Environment.CurrentDirectory, "sql", "schema.sql")
		};

		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		for (var i = 0; i < 6 && directory != null; i++)
		{
			candidates.Add(Path.Combine(directory.FullName, "server", "sql", "schema.sql"));
			directory = directory.Parent;
		}

		return FindFirstExistingDistinctPathAlgorithm.Run(candidates);
	}
}
