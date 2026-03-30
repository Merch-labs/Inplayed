using System.IO;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace inplayed;

internal sealed class SocialDatabaseStore
{
	private readonly string _connectionString;

	public bool IsLoggedIn => SocialAuthSession.IsLoggedIn;
	public string CurrentUsername => SocialAuthSession.Username;
	public string CurrentDisplayName => SocialAuthSession.DisplayName;

	public static bool TryConnect(string connectionString, string username, out string message)
	{
		message = string.Empty;

		var trimmedConnectionString = connectionString?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(trimmedConnectionString))
		{
			message = "Enter a database connection string first.";
			return false;
		}

		try
		{
			_ = new SocialDatabaseStore(trimmedConnectionString);
			message = "Connected to the social database.";
			return true;
		}
		catch (Exception ex)
		{
			message = ex.Message;
			return false;
		}
	}

	public SocialDatabaseStore(string connectionString)
	{
		_connectionString = connectionString.Trim();
		EnsureCreated();
	}

	public bool CreateAccount(string rawUsername, string rawDisplayName, string rawPassword, out string message)
	{
		message = string.Empty;

		var username = NormalizeUsername(rawUsername);
		var displayName = NormalizeDisplayName(rawDisplayName, username);
		var password = rawPassword?.Trim() ?? string.Empty;

		if (!IsValidUsername(username))
		{
			message = "Choose a username with 3 to 50 letters, numbers, or underscores.";
			return false;
		}

		if (password.Length < 4)
		{
			message = "Password must be at least 4 characters.";
			return false;
		}

		var passwordHash = HashPassword(password);

		using var connection = CreateOpenConnection();
		using var findCommand = new NpgsqlCommand(
			"SELECT id, password_hash FROM users WHERE LOWER(username) = LOWER(@username)",
			connection);
		findCommand.Parameters.AddWithValue("username", username);

		using var reader = findCommand.ExecuteReader();
		if (reader.Read())
		{
			var existingId = reader.GetInt32(0);
			var existingPasswordHash = reader.GetString(1);
			reader.Close();

			if (!string.IsNullOrWhiteSpace(existingPasswordHash))
			{
				message = "That username already exists.";
				return false;
			}

			using var updateCommand = new NpgsqlCommand(
				@"UPDATE users
				  SET display_name = @displayName, password_hash = @passwordHash
				  WHERE id = @id",
				connection);
			updateCommand.Parameters.AddWithValue("displayName", displayName);
			updateCommand.Parameters.AddWithValue("passwordHash", passwordHash);
			updateCommand.Parameters.AddWithValue("id", existingId);
			updateCommand.ExecuteNonQuery();

			SocialAuthSession.Set(existingId, username, displayName);
			message = "Account created and logged in.";
			return true;
		}

		reader.Close();

		using var insertCommand = new NpgsqlCommand(
			@"INSERT INTO users (username, display_name, password_hash, created_at_utc)
			  VALUES (@username, @displayName, @passwordHash, @createdAtUtc)
			  RETURNING id",
			connection);
		insertCommand.Parameters.AddWithValue("username", username);
		insertCommand.Parameters.AddWithValue("displayName", displayName);
		insertCommand.Parameters.AddWithValue("passwordHash", passwordHash);
		insertCommand.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);

		var id = Convert.ToInt32(insertCommand.ExecuteScalar());
		SocialAuthSession.Set(id, username, displayName);
		message = "Account created and logged in.";
		return true;
	}

	public bool Login(string rawUsername, string rawPassword, out string message)
	{
		message = string.Empty;

		var username = NormalizeUsername(rawUsername);
		var password = rawPassword?.Trim() ?? string.Empty;
		if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
		{
			message = "Enter a username and password.";
			return false;
		}

		using var connection = CreateOpenConnection();
		using var command = new NpgsqlCommand(
			"SELECT id, username, display_name, password_hash FROM users WHERE LOWER(username) = LOWER(@username)",
			connection);
		command.Parameters.AddWithValue("username", username);

		using var reader = command.ExecuteReader();
		if (!reader.Read())
		{
			message = "Account not found.";
			return false;
		}

		var userId = reader.GetInt32(0);
		var actualUsername = reader.GetString(1);
		var displayName = reader.GetString(2);
		var passwordHash = reader.GetString(3);

		if (string.IsNullOrWhiteSpace(passwordHash))
		{
			message = "This account does not have a password yet.";
			return false;
		}

		if (!string.Equals(passwordHash, HashPassword(password), StringComparison.Ordinal))
		{
			message = "Password is incorrect.";
			return false;
		}

		SocialAuthSession.Set(userId, actualUsername, displayName);
		message = "Logged in.";
		return true;
	}

	public void Logout()
	{
		SocialAuthSession.Clear();
	}

	public IReadOnlyList<string> GetFriends()
	{
		if (!TryGetCurrentUserId(out var currentUserId))
		{
			return [];
		}

		var friends = new List<string>();

		using var connection = CreateOpenConnection();
		using var command = new NpgsqlCommand(
			@"SELECT u.display_name
			  FROM friend_requests fr
			  JOIN users u
			    ON u.id =
			        CASE
			            WHEN fr.requester_id = @currentUserId THEN fr.recipient_id
			            ELSE fr.requester_id
			        END
			  WHERE fr.status = 'Accepted'
			    AND (fr.requester_id = @currentUserId OR fr.recipient_id = @currentUserId)
			  ORDER BY u.display_name",
			connection);
		command.Parameters.AddWithValue("currentUserId", currentUserId);

		using var reader = command.ExecuteReader();
		while (reader.Read())
		{
			friends.Add(reader.GetString(0));
		}

		return friends;
	}

	public IReadOnlyList<SocialFriendRequest> GetPendingFriendRequests()
	{
		if (!TryGetCurrentUserId(out var currentUserId))
		{
			return [];
		}

		var requests = new List<SocialFriendRequest>();

		using var connection = CreateOpenConnection();
		using var command = new NpgsqlCommand(
			@"SELECT fr.id, u.username, u.display_name, fr.created_at_utc
			  FROM friend_requests fr
			  JOIN users u ON u.id = fr.requester_id
			  WHERE fr.recipient_id = @currentUserId
			    AND fr.status = 'Pending'
			  ORDER BY fr.created_at_utc",
			connection);
		command.Parameters.AddWithValue("currentUserId", currentUserId);

		using var reader = command.ExecuteReader();
		while (reader.Read())
		{
			requests.Add(new SocialFriendRequest
			{
				Id = reader.GetInt32(0),
				Username = reader.GetString(1),
				DisplayName = reader.GetString(2),
				CreatedAtUtc = reader.GetDateTime(3)
			});
		}

		return requests;
	}

	public bool SendFriendRequest(string rawUsername, out string normalizedUsername, out string message)
	{
		normalizedUsername = NormalizeUsername(rawUsername);
		message = string.Empty;

		if (!TryGetCurrentUserId(out var currentUserId))
		{
			message = "Log in first.";
			return false;
		}

		if (!IsValidUsername(normalizedUsername))
		{
			message = "Enter an existing username.";
			return false;
		}

		if (string.Equals(normalizedUsername, CurrentUsername, StringComparison.OrdinalIgnoreCase))
		{
			message = "You cannot add yourself.";
			return false;
		}

		using var connection = CreateOpenConnection();
		var friendId = FindUserIdByUsername(connection, normalizedUsername);
		if (friendId <= 0)
		{
			message = "That user does not exist.";
			return false;
		}

		using var existingCommand = new NpgsqlCommand(
			@"SELECT id, requester_id, recipient_id, status
			  FROM friend_requests
			  WHERE (requester_id = @currentUserId AND recipient_id = @friendId)
			     OR (requester_id = @friendId AND recipient_id = @currentUserId)",
			connection);
		existingCommand.Parameters.AddWithValue("currentUserId", currentUserId);
		existingCommand.Parameters.AddWithValue("friendId", friendId);

		using var reader = existingCommand.ExecuteReader();
		if (reader.Read())
		{
			var requestId = reader.GetInt32(0);
			var requesterId = reader.GetInt32(1);
			var recipientId = reader.GetInt32(2);
			var status = reader.GetString(3);
			reader.Close();

			if (string.Equals(status, "Accepted", StringComparison.OrdinalIgnoreCase))
			{
				message = "You are already friends.";
				return false;
			}

			if (string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
			{
				if (requesterId == currentUserId)
				{
					message = "Friend request already sent.";
					return false;
				}

				message = "That user has already sent you a request.";
				return false;
			}

			using var updateCommand = new NpgsqlCommand(
				@"UPDATE friend_requests
				  SET requester_id = @currentUserId,
				      recipient_id = @friendId,
				      status = @status,
				      created_at_utc = @createdAtUtc,
				      responded_at_utc = @respondedAtUtc
				  WHERE id = @id",
				connection);
			updateCommand.Parameters.AddWithValue("currentUserId", currentUserId);
			updateCommand.Parameters.AddWithValue("friendId", friendId);
			updateCommand.Parameters.AddWithValue("status", "Pending");
			updateCommand.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);
			updateCommand.Parameters.AddWithValue("respondedAtUtc", DBNull.Value);
			updateCommand.Parameters.AddWithValue("id", requestId);
			updateCommand.ExecuteNonQuery();

			message = $"Friend request sent to @{normalizedUsername}.";
			return true;
		}

		reader.Close();

		using var command = new NpgsqlCommand(
			@"INSERT INTO friend_requests
			  (requester_id, recipient_id, status, created_at_utc, responded_at_utc)
			  VALUES (@currentUserId, @friendId, @status, @createdAtUtc, @respondedAtUtc)",
			connection);
		command.Parameters.AddWithValue("currentUserId", currentUserId);
		command.Parameters.AddWithValue("friendId", friendId);
		command.Parameters.AddWithValue("status", "Pending");
		command.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);
		command.Parameters.AddWithValue("respondedAtUtc", DBNull.Value);
		command.ExecuteNonQuery();

		message = $"Friend request sent to @{normalizedUsername}.";
		return true;
	}

	public bool AcceptFriendRequest(int requestId, out string message)
	{
		message = string.Empty;

		if (!TryGetCurrentUserId(out var currentUserId))
		{
			message = "Log in first.";
			return false;
		}

		using var connection = CreateOpenConnection();
		using var updateCommand = new NpgsqlCommand(
			@"UPDATE friend_requests
			  SET status = @status, responded_at_utc = @respondedAtUtc
			  WHERE id = @id AND recipient_id = @currentUserId AND status = 'Pending'",
			connection);
		updateCommand.Parameters.AddWithValue("status", "Accepted");
		updateCommand.Parameters.AddWithValue("respondedAtUtc", DateTime.UtcNow);
		updateCommand.Parameters.AddWithValue("id", requestId);
		updateCommand.Parameters.AddWithValue("currentUserId", currentUserId);

		if (updateCommand.ExecuteNonQuery() <= 0)
		{
			message = "Friend request not found.";
			return false;
		}

		message = "Friend request accepted.";
		return true;
	}

	public bool RejectFriendRequest(int requestId, out string message)
	{
		message = string.Empty;

		if (!TryGetCurrentUserId(out var currentUserId))
		{
			message = "Log in first.";
			return false;
		}

		using var connection = CreateOpenConnection();
		using var updateCommand = new NpgsqlCommand(
			@"UPDATE friend_requests
			  SET status = @status, responded_at_utc = @respondedAtUtc
			  WHERE id = @id AND recipient_id = @currentUserId AND status = 'Pending'",
			connection);
		updateCommand.Parameters.AddWithValue("status", "Rejected");
		updateCommand.Parameters.AddWithValue("respondedAtUtc", DateTime.UtcNow);
		updateCommand.Parameters.AddWithValue("id", requestId);
		updateCommand.Parameters.AddWithValue("currentUserId", currentUserId);

		if (updateCommand.ExecuteNonQuery() <= 0)
		{
			message = "Friend request not found.";
			return false;
		}

		message = "Friend request rejected.";
		return true;
	}

	public IReadOnlyList<SocialMessage> GetMessages(string friendName)
	{
		if (!TryGetCurrentUserId(out var currentUserId))
		{
			return [];
		}

		var friendUserId = FindAcceptedFriendId(currentUserId, friendName);
		if (friendUserId <= 0)
		{
			return [];
		}

		var conversationId = FindDirectConversationId(currentUserId, friendUserId);
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
				Author = senderId == currentUserId ? "You" : reader.GetString(2),
				Kind = isClip ? SocialMessageKinds.Clip : SocialMessageKinds.Text,
				Body = reader.GetString(3),
				ClipPath = isClip ? mediaUrl : string.Empty,
				ClipFileName = isClip ? Path.GetFileName(mediaUrl) : string.Empty,
				CreatedAtUtc = reader.GetDateTime(6)
			});
		}

		return messages;
	}

	public bool RemoveFriend(string friendName)
	{
		if (!TryGetCurrentUserId(out var currentUserId))
		{
			return false;
		}

		var friendId = FindAcceptedFriendId(currentUserId, friendName);
		if (friendId <= 0)
		{
			return false;
		}

		var conversationId = FindDirectConversationId(currentUserId, friendId);

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
			  WHERE status = 'Accepted'
			    AND ((requester_id = @currentUserId AND recipient_id = @friendId)
			      OR (requester_id = @friendId AND recipient_id = @currentUserId))",
			connection);
		deleteFriendship.Parameters.AddWithValue("currentUserId", currentUserId);
		deleteFriendship.Parameters.AddWithValue("friendId", friendId);

		return deleteFriendship.ExecuteNonQuery() > 0;
	}

	public SocialMessage? SendTextMessage(string friendName, string rawMessage)
	{
		if (!TryGetCurrentUserId(out var currentUserId))
		{
			return null;
		}

		var messageBody = rawMessage.Trim();
		if (string.IsNullOrWhiteSpace(messageBody))
		{
			return null;
		}

		var friendId = FindAcceptedFriendId(currentUserId, friendName);
		if (friendId <= 0)
		{
			return null;
		}

		var conversationId = EnsureConversation(currentUserId, friendId);
		var createdAtUtc = DateTime.UtcNow;

		using var connection = CreateOpenConnection();
		using var command = new NpgsqlCommand(
			@"INSERT INTO messages
			  (conversation_id, sender_id, body, media_url, media_type, created_at_utc)
			  VALUES (@conversationId, @senderId, @body, @mediaUrl, @mediaType, @createdAtUtc)
			  RETURNING id",
			connection);
		command.Parameters.AddWithValue("conversationId", conversationId);
		command.Parameters.AddWithValue("senderId", currentUserId);
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
		if (!TryGetCurrentUserId(out var currentUserId))
		{
			return null;
		}

		if (string.IsNullOrWhiteSpace(clipPath))
		{
			return null;
		}

		var friendId = FindAcceptedFriendId(currentUserId, friendName);
		if (friendId <= 0)
		{
			return null;
		}

		var conversationId = EnsureConversation(currentUserId, friendId);
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
		command.Parameters.AddWithValue("senderId", currentUserId);
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
		using var tableCommand = new NpgsqlCommand(
			"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users'",
			connection);
		var tableCount = Convert.ToInt32(tableCommand.ExecuteScalar());
		if (tableCount <= 0)
		{
			var schemaPath = ResolveSchemaPath();
			if (string.IsNullOrWhiteSpace(schemaPath) || !File.Exists(schemaPath))
			{
				throw new FileNotFoundException("Could not find social database schema file.", schemaPath);
			}

			var sql = File.ReadAllText(schemaPath);
			using var createCommand = new NpgsqlCommand(sql, connection);
			createCommand.ExecuteNonQuery();
			return;
		}

		using var columnCommand = new NpgsqlCommand(
			"SELECT COUNT(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'users' AND column_name = 'password_hash'",
			connection);
		var columnCount = Convert.ToInt32(columnCommand.ExecuteScalar());
		if (columnCount <= 0)
		{
			using var alterCommand = new NpgsqlCommand(
				"ALTER TABLE users ADD COLUMN password_hash VARCHAR(64) NOT NULL DEFAULT ''",
				connection);
			alterCommand.ExecuteNonQuery();
		}
	}

	private NpgsqlConnection CreateOpenConnection()
	{
		var connection = new NpgsqlConnection(_connectionString);
		connection.Open();
		return connection;
	}

	private bool TryGetCurrentUserId(out int currentUserId)
	{
		currentUserId = 0;
		if (!SocialAuthSession.IsLoggedIn)
		{
			return false;
		}

		currentUserId = SocialAuthSession.UserId;
		return currentUserId > 0;
	}

	private int FindAcceptedFriendId(int currentUserId, string friendName)
	{
		var normalizedUsername = NormalizeUsername(friendName);
		var normalizedDisplayName = NormalizeDisplayName(friendName, normalizedUsername);
		if (string.IsNullOrWhiteSpace(normalizedUsername) && string.IsNullOrWhiteSpace(normalizedDisplayName))
		{
			return 0;
		}

		using var connection = CreateOpenConnection();
		using var command = new NpgsqlCommand(
			@"SELECT u.id
			  FROM friend_requests fr
			  JOIN users u
			    ON u.id =
			        CASE
			            WHEN fr.requester_id = @currentUserId THEN fr.recipient_id
			            ELSE fr.requester_id
			        END
			  WHERE fr.status = 'Accepted'
			    AND (fr.requester_id = @currentUserId OR fr.recipient_id = @currentUserId)
			    AND (LOWER(u.username) = LOWER(@username) OR LOWER(u.display_name) = LOWER(@displayName))",
			connection);
		command.Parameters.AddWithValue("currentUserId", currentUserId);
		command.Parameters.AddWithValue("username", normalizedUsername);
		command.Parameters.AddWithValue("displayName", normalizedDisplayName);

		var result = command.ExecuteScalar();
		if (result == null || result == DBNull.Value)
		{
			return 0;
		}

		return Convert.ToInt32(result);
	}

	private int FindUserIdByUsername(NpgsqlConnection connection, string username)
	{
		using var command = new NpgsqlCommand(
			"SELECT id FROM users WHERE LOWER(username) = LOWER(@username)",
			connection);
		command.Parameters.AddWithValue("username", username);

		var result = command.ExecuteScalar();
		if (result == null || result == DBNull.Value)
		{
			return 0;
		}

		return Convert.ToInt32(result);
	}

	private int FindDirectConversationId(int currentUserId, int friendId)
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
		command.Parameters.AddWithValue("currentUserId", currentUserId);
		command.Parameters.AddWithValue("friendId", friendId);

		var result = command.ExecuteScalar();
		if (result == null || result == DBNull.Value)
		{
			return 0;
		}

		return Convert.ToInt32(result);
	}

	private int EnsureConversation(int currentUserId, int friendId)
	{
		var existingId = FindDirectConversationId(currentUserId, friendId);
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

		var memberIds = new[] { currentUserId, friendId };
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

	private static bool IsValidUsername(string username)
	{
		if (string.IsNullOrWhiteSpace(username) || username.Length < 3 || username.Length > 50)
		{
			return false;
		}

		foreach (var character in username)
		{
			if (!char.IsLetterOrDigit(character) && character != '_')
			{
				return false;
			}
		}

		return true;
	}

	private static string NormalizeUsername(string? value)
	{
		return value?.Trim().ToLowerInvariant() ?? string.Empty;
	}

	private static string NormalizeDisplayName(string? value, string username)
	{
		var displayName = value?.Trim() ?? string.Empty;
		return string.IsNullOrWhiteSpace(displayName) ? username : displayName;
	}

	private static string HashPassword(string password)
	{
		var bytes = Encoding.UTF8.GetBytes(password);
		using var algorithm = SHA256.Create();
		var hash = algorithm.ComputeHash(bytes);
		return Convert.ToHexString(hash);
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
