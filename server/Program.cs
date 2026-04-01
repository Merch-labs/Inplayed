using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
	builder.WebHost.UseUrls("http://0.0.0.0:5167");
}

var app = builder.Build();
var databasePath = Path.Combine(AppContext.BaseDirectory, "social.db");
var connectionString = new SqliteConnectionStringBuilder
{
	DataSource = databasePath
}.ToString();
var sessionTokens = new Dictionary<string, SessionUser>(StringComparer.Ordinal);
var sessionLock = new object();

EnsureDatabaseReady(connectionString);

app.MapGet("/", () => Results.Ok(new { message = "inplayed social server is running" }));

app.MapPost("/api/auth/register", (RegisterRequest request) =>
{
	var username = (request.Username ?? string.Empty).Trim();
	var displayName = (request.DisplayName ?? string.Empty).Trim();
	var password = request.Password ?? string.Empty;
	if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
	{
		return Results.BadRequest(new MessageResponse("Username must be at least 3 characters."));
	}

	if (string.IsNullOrWhiteSpace(displayName))
	{
		displayName = username;
	}

	if (password.Length < 4)
	{
		return Results.BadRequest(new MessageResponse("Password must be at least 4 characters."));
	}

	using var connection = OpenConnection(connectionString);
	if (TryGetUserByUsername(connection, username, out _, out _, out _))
	{
		return Results.BadRequest(new MessageResponse("That username already exists."));
	}

	using var command = connection.CreateCommand();
	command.CommandText = @"
		INSERT INTO users (username, display_name, password_hash, created_at_utc)
		VALUES (@username, @displayName, @passwordHash, @createdAtUtc);
		SELECT last_insert_rowid();";
	command.Parameters.AddWithValue("@username", username);
	command.Parameters.AddWithValue("@displayName", displayName);
	command.Parameters.AddWithValue("@passwordHash", HashPassword(password));
	command.Parameters.AddWithValue("@createdAtUtc", DateTime.UtcNow.ToString("O"));
	var userId = Convert.ToInt32(command.ExecuteScalar());
	var token = CreateSessionToken(userId, username, displayName, sessionTokens, sessionLock);

	return Results.Ok(new AuthResponse(token, username, displayName));
});

app.MapPost("/api/auth/login", (LoginRequest request) =>
{
	var username = (request.Username ?? string.Empty).Trim();
	var password = request.Password ?? string.Empty;
	if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
	{
		return Results.BadRequest(new MessageResponse("Enter your username and password."));
	}

	using var connection = OpenConnection(connectionString);
	if (!TryGetUserByUsername(connection, username, out var userId, out var actualUsername, out var displayName))
	{
		return Results.BadRequest(new MessageResponse("That account does not exist."));
	}

	using var command = connection.CreateCommand();
	command.CommandText = "SELECT password_hash FROM users WHERE id = @userId;";
	command.Parameters.AddWithValue("@userId", userId);
	var passwordHash = command.ExecuteScalar()?.ToString() ?? string.Empty;
	if (!string.Equals(passwordHash, HashPassword(password), StringComparison.Ordinal))
	{
		return Results.BadRequest(new MessageResponse("Password was incorrect."));
	}

	var token = CreateSessionToken(userId, actualUsername, displayName, sessionTokens, sessionLock);
	return Results.Ok(new AuthResponse(token, actualUsername, displayName));
});

app.MapPost("/api/auth/logout", (HttpRequest httpRequest) =>
{
	var token = ReadSessionToken(httpRequest);
	if (!string.IsNullOrWhiteSpace(token))
	{
		lock (sessionLock)
		{
			sessionTokens.Remove(token);
		}
	}

	return Results.Ok(new MessageResponse("Logged out."));
});

app.MapGet("/api/friends", (HttpRequest httpRequest) =>
{
	if (!TryGetSessionUser(httpRequest, sessionTokens, sessionLock, out var sessionUser, out var errorResult))
	{
		return errorResult;
	}

	var friends = new List<FriendResponse>();
	using var connection = OpenConnection(connectionString);
	using var command = connection.CreateCommand();
	command.CommandText = @"
		SELECT username
		FROM users
		WHERE id IN
		(
			SELECT recipient_user_id FROM friend_requests WHERE requester_user_id = @userId AND status = 'accepted'
			UNION
			SELECT requester_user_id FROM friend_requests WHERE recipient_user_id = @userId AND status = 'accepted'
		)
		ORDER BY username;";
	command.Parameters.AddWithValue("@userId", sessionUser.UserId);
	using var reader = command.ExecuteReader();
	while (reader.Read())
	{
		friends.Add(new FriendResponse(reader.GetString(0)));
	}

	return Results.Ok(friends);
});

app.MapGet("/api/friend-requests/pending", (HttpRequest httpRequest) =>
{
	if (!TryGetSessionUser(httpRequest, sessionTokens, sessionLock, out var sessionUser, out var errorResult))
	{
		return errorResult;
	}

	var requests = new List<FriendRequestResponse>();
	using var connection = OpenConnection(connectionString);
	using var command = connection.CreateCommand();
	command.CommandText = @"
		SELECT fr.id, u.username, u.display_name, fr.created_at_utc
		FROM friend_requests fr
		INNER JOIN users u ON u.id = fr.requester_user_id
		WHERE fr.recipient_user_id = @userId AND fr.status = 'pending'
		ORDER BY fr.created_at_utc;";
	command.Parameters.AddWithValue("@userId", sessionUser.UserId);
	using var reader = command.ExecuteReader();
	while (reader.Read())
	{
		requests.Add(new FriendRequestResponse(
			reader.GetInt32(0),
			reader.GetString(1),
			reader.GetString(2),
			DateTime.Parse(reader.GetString(3)).ToUniversalTime()));
	}

	return Results.Ok(requests);
});

app.MapPost("/api/friend-requests", (HttpRequest httpRequest, SendFriendRequestRequest request) =>
{
	if (!TryGetSessionUser(httpRequest, sessionTokens, sessionLock, out var sessionUser, out var errorResult))
	{
		return errorResult;
	}

	var username = (request.Username ?? string.Empty).Trim();
	if (string.IsNullOrWhiteSpace(username))
	{
		return Results.BadRequest(new MessageResponse("Enter a username first."));
	}

	using var connection = OpenConnection(connectionString);
	if (!TryGetUserByUsername(connection, username, out var targetUserId, out var actualUsername, out _))
	{
		return Results.BadRequest(new MessageResponse("That user does not exist."));
	}

	if (targetUserId == sessionUser.UserId)
	{
		return Results.BadRequest(new MessageResponse("You cannot add yourself."));
	}

	if (AreFriends(connection, sessionUser.UserId, targetUserId))
	{
		return Results.BadRequest(new MessageResponse("You are already friends."));
	}

	if (HasPendingFriendRequest(connection, sessionUser.UserId, targetUserId))
	{
		return Results.BadRequest(new MessageResponse("There is already a pending friend request."));
	}

	using var command = connection.CreateCommand();
	command.CommandText = @"
		INSERT INTO friend_requests (requester_user_id, recipient_user_id, status, created_at_utc, responded_at_utc)
		VALUES (@requesterId, @recipientId, 'pending', @createdAtUtc, '');";
	command.Parameters.AddWithValue("@requesterId", sessionUser.UserId);
	command.Parameters.AddWithValue("@recipientId", targetUserId);
	command.Parameters.AddWithValue("@createdAtUtc", DateTime.UtcNow.ToString("O"));
	command.ExecuteNonQuery();

	return Results.Ok(new FriendRequestSendResponse(actualUsername, $"Sent request to @{actualUsername}."));
});

app.MapPost("/api/friend-requests/{requestId:int}/accept", (HttpRequest httpRequest, int requestId) =>
{
	if (!TryGetSessionUser(httpRequest, sessionTokens, sessionLock, out var sessionUser, out var errorResult))
	{
		return errorResult;
	}

	using var connection = OpenConnection(connectionString);
	using var command = connection.CreateCommand();
	command.CommandText = @"
		UPDATE friend_requests
		SET status = 'accepted', responded_at_utc = @respondedAtUtc
		WHERE id = @requestId AND recipient_user_id = @userId AND status = 'pending';";
	command.Parameters.AddWithValue("@requestId", requestId);
	command.Parameters.AddWithValue("@userId", sessionUser.UserId);
	command.Parameters.AddWithValue("@respondedAtUtc", DateTime.UtcNow.ToString("O"));
	var changed = command.ExecuteNonQuery();
	if (changed == 0)
	{
		return Results.BadRequest(new MessageResponse("Friend request could not be accepted."));
	}

	return Results.Ok(new MessageResponse("Friend request accepted."));
});

app.MapPost("/api/friend-requests/{requestId:int}/reject", (HttpRequest httpRequest, int requestId) =>
{
	if (!TryGetSessionUser(httpRequest, sessionTokens, sessionLock, out var sessionUser, out var errorResult))
	{
		return errorResult;
	}

	using var connection = OpenConnection(connectionString);
	using var command = connection.CreateCommand();
	command.CommandText = @"
		UPDATE friend_requests
		SET status = 'rejected', responded_at_utc = @respondedAtUtc
		WHERE id = @requestId AND recipient_user_id = @userId AND status = 'pending';";
	command.Parameters.AddWithValue("@requestId", requestId);
	command.Parameters.AddWithValue("@userId", sessionUser.UserId);
	command.Parameters.AddWithValue("@respondedAtUtc", DateTime.UtcNow.ToString("O"));
	var changed = command.ExecuteNonQuery();
	if (changed == 0)
	{
		return Results.BadRequest(new MessageResponse("Friend request could not be rejected."));
	}

	return Results.Ok(new MessageResponse("Friend request rejected."));
});

app.MapDelete("/api/friends/{username}", (HttpRequest httpRequest, string username) =>
{
	if (!TryGetSessionUser(httpRequest, sessionTokens, sessionLock, out var sessionUser, out var errorResult))
	{
		return errorResult;
	}

	using var connection = OpenConnection(connectionString);
	if (!TryGetUserByUsername(connection, username, out var friendUserId, out _, out _))
	{
		return Results.BadRequest(new MessageResponse("That user does not exist."));
	}

	using var command = connection.CreateCommand();
	command.CommandText = @"
		DELETE FROM friend_requests
		WHERE status = 'accepted'
		AND ((requester_user_id = @userId AND recipient_user_id = @friendUserId)
		OR (requester_user_id = @friendUserId AND recipient_user_id = @userId));";
	command.Parameters.AddWithValue("@userId", sessionUser.UserId);
	command.Parameters.AddWithValue("@friendUserId", friendUserId);
	var changed = command.ExecuteNonQuery();
	if (changed == 0)
	{
		return Results.BadRequest(new MessageResponse("That person is not in your friends list."));
	}

	return Results.Ok(new MessageResponse("Friend removed."));
});

app.MapGet("/api/messages/{friendUsername}", (HttpRequest httpRequest, string friendUsername) =>
{
	if (!TryGetSessionUser(httpRequest, sessionTokens, sessionLock, out var sessionUser, out var errorResult))
	{
		return errorResult;
	}

	using var connection = OpenConnection(connectionString);
	if (!TryGetUserByUsername(connection, friendUsername, out var friendUserId, out _, out _))
	{
		return Results.BadRequest(new MessageResponse("That user does not exist."));
	}

	if (!AreFriends(connection, sessionUser.UserId, friendUserId))
	{
		return Results.BadRequest(new MessageResponse("You can only message accepted friends."));
	}

	var messages = new List<MessageResponseDto>();
	using var command = connection.CreateCommand();
	command.CommandText = @"
		SELECT m.id, sender.username, m.kind, m.body, m.clip_path, m.clip_file_name, m.created_at_utc
		FROM messages m
		INNER JOIN users sender ON sender.id = m.sender_user_id
		WHERE (m.sender_user_id = @userId AND m.recipient_user_id = @friendUserId)
		OR (m.sender_user_id = @friendUserId AND m.recipient_user_id = @userId)
		ORDER BY m.created_at_utc;";
	command.Parameters.AddWithValue("@userId", sessionUser.UserId);
	command.Parameters.AddWithValue("@friendUserId", friendUserId);
	using var reader = command.ExecuteReader();
	while (reader.Read())
	{
		messages.Add(new MessageResponseDto(
			reader.GetInt32(0),
			reader.GetString(1),
			reader.GetString(2),
			reader.GetString(3),
			reader.GetString(4),
			reader.GetString(5),
			DateTime.Parse(reader.GetString(6)).ToUniversalTime()));
	}

	return Results.Ok(messages);
});

app.MapPost("/api/messages/{friendUsername}", (HttpRequest httpRequest, string friendUsername, SendMessageRequest request) =>
{
	if (!TryGetSessionUser(httpRequest, sessionTokens, sessionLock, out var sessionUser, out var errorResult))
	{
		return errorResult;
	}

	using var connection = OpenConnection(connectionString);
	if (!TryGetUserByUsername(connection, friendUsername, out var friendUserId, out _, out _))
	{
		return Results.BadRequest(new MessageResponse("That user does not exist."));
	}

	if (!AreFriends(connection, sessionUser.UserId, friendUserId))
	{
		return Results.BadRequest(new MessageResponse("You can only message accepted friends."));
	}

	var kind = (request.Kind ?? string.Empty).Trim().ToLowerInvariant();
	if (kind != "text" && kind != "clip")
	{
		return Results.BadRequest(new MessageResponse("Message type was not valid."));
	}

	var body = (request.Body ?? string.Empty).Trim();
	var clipPath = request.ClipPath ?? string.Empty;
	var clipFileName = request.ClipFileName ?? string.Empty;
	if (kind == "text" && string.IsNullOrWhiteSpace(body))
	{
		return Results.BadRequest(new MessageResponse("Write a message first."));
	}

	if (kind == "clip" && string.IsNullOrWhiteSpace(clipPath))
	{
		return Results.BadRequest(new MessageResponse("Choose a clip first."));
	}

	if (kind == "clip" && string.IsNullOrWhiteSpace(body))
	{
		body = "Shared a clip";
	}

	using var command = connection.CreateCommand();
	command.CommandText = @"
		INSERT INTO messages (sender_user_id, recipient_user_id, kind, body, clip_path, clip_file_name, created_at_utc)
		VALUES (@senderUserId, @recipientUserId, @kind, @body, @clipPath, @clipFileName, @createdAtUtc);
		SELECT last_insert_rowid();";
	command.Parameters.AddWithValue("@senderUserId", sessionUser.UserId);
	command.Parameters.AddWithValue("@recipientUserId", friendUserId);
	command.Parameters.AddWithValue("@kind", kind);
	command.Parameters.AddWithValue("@body", body);
	command.Parameters.AddWithValue("@clipPath", clipPath);
	command.Parameters.AddWithValue("@clipFileName", clipFileName);
	command.Parameters.AddWithValue("@createdAtUtc", DateTime.UtcNow.ToString("O"));
	var messageId = Convert.ToInt32(command.ExecuteScalar());

	return Results.Ok(new MessageResponseDto(
		messageId,
		sessionUser.Username,
		kind,
		body,
		clipPath,
		clipFileName,
		DateTime.UtcNow));
});

app.Run();

static void EnsureDatabaseReady(string connectionString)
{
	using var connection = OpenConnection(connectionString);
	if (TableExists(connection, "users"))
	{
		return;
	}

	var schemaPath = Path.Combine(AppContext.BaseDirectory, "sql", "schema.sql");
	if (!File.Exists(schemaPath))
	{
		throw new FileNotFoundException("Could not find sql/schema.sql for server setup.", schemaPath);
	}

	using var command = connection.CreateCommand();
	command.CommandText = File.ReadAllText(schemaPath);
	command.ExecuteNonQuery();
}

static bool TableExists(SqliteConnection connection, string tableName)
{
	using var command = connection.CreateCommand();
	command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = @tableName;";
	command.Parameters.AddWithValue("@tableName", tableName);
	return command.ExecuteScalar() != null;
}

static SqliteConnection OpenConnection(string connectionString)
{
	var connection = new SqliteConnection(connectionString);
	connection.Open();
	return connection;
}

static bool TryGetSessionUser(HttpRequest httpRequest, Dictionary<string, SessionUser> sessionTokens, object sessionLock, out SessionUser sessionUser, out IResult errorResult)
{
	sessionUser = new SessionUser(0, string.Empty, string.Empty);
	errorResult = Results.Unauthorized();
	var token = ReadSessionToken(httpRequest);
	if (string.IsNullOrWhiteSpace(token))
	{
		return false;
	}

	lock (sessionLock)
	{
		if (!sessionTokens.TryGetValue(token, out var foundUser))
		{
			return false;
		}

		sessionUser = foundUser;
	}

	return true;
}

static string ReadSessionToken(HttpRequest httpRequest)
{
	var header = httpRequest.Headers.Authorization.ToString();
	if (string.IsNullOrWhiteSpace(header))
	{
		return string.Empty;
	}

	const string prefix = "Bearer ";
	if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
	{
		return string.Empty;
	}

	return header[prefix.Length..].Trim();
}

static string CreateSessionToken(int userId, string username, string displayName, Dictionary<string, SessionUser> sessionTokens, object sessionLock)
{
	var token = Guid.NewGuid().ToString("N");
	lock (sessionLock)
	{
		sessionTokens[token] = new SessionUser(userId, username, displayName);
	}

	return token;
}

static bool TryGetUserByUsername(SqliteConnection connection, string username, out int userId, out string actualUsername, out string displayName)
{
	userId = 0;
	actualUsername = string.Empty;
	displayName = string.Empty;
	using var command = connection.CreateCommand();
	command.CommandText = "SELECT id, username, display_name FROM users WHERE username = @username;";
	command.Parameters.AddWithValue("@username", username.Trim());
	using var reader = command.ExecuteReader();
	if (!reader.Read())
	{
		return false;
	}

	userId = reader.GetInt32(0);
	actualUsername = reader.GetString(1);
	displayName = reader.GetString(2);
	return true;
}

static bool AreFriends(SqliteConnection connection, int firstUserId, int secondUserId)
{
	using var command = connection.CreateCommand();
	command.CommandText = @"
		SELECT id FROM friend_requests
		WHERE status = 'accepted'
		AND ((requester_user_id = @firstUserId AND recipient_user_id = @secondUserId)
		OR (requester_user_id = @secondUserId AND recipient_user_id = @firstUserId));";
	command.Parameters.AddWithValue("@firstUserId", firstUserId);
	command.Parameters.AddWithValue("@secondUserId", secondUserId);
	return command.ExecuteScalar() != null;
}

static bool HasPendingFriendRequest(SqliteConnection connection, int firstUserId, int secondUserId)
{
	using var command = connection.CreateCommand();
	command.CommandText = @"
		SELECT id FROM friend_requests
		WHERE status = 'pending'
		AND ((requester_user_id = @firstUserId AND recipient_user_id = @secondUserId)
		OR (requester_user_id = @secondUserId AND recipient_user_id = @firstUserId));";
	command.Parameters.AddWithValue("@firstUserId", firstUserId);
	command.Parameters.AddWithValue("@secondUserId", secondUserId);
	return command.ExecuteScalar() != null;
}

static string HashPassword(string password)
{
	var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
	return Convert.ToHexString(bytes);
}

sealed record RegisterRequest(string Username, string DisplayName, string Password);
sealed record LoginRequest(string Username, string Password);
sealed record SendFriendRequestRequest(string Username);
sealed record SendMessageRequest(string Kind, string Body, string ClipPath, string ClipFileName);
sealed record MessageResponse(string Message);
sealed record AuthResponse(string SessionToken, string Username, string DisplayName);
sealed record FriendResponse(string Username);
sealed record FriendRequestResponse(int Id, string Username, string DisplayName, DateTime CreatedAtUtc);
sealed record FriendRequestSendResponse(string Username, string Message);
sealed record MessageResponseDto(int Id, string Author, string Kind, string Body, string ClipPath, string ClipFileName, DateTime CreatedAtUtc);
sealed record SessionUser(int UserId, string Username, string DisplayName);
