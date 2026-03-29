using inplayed.Server.Data;
using inplayed.Server.Models;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SocialDatabase>();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

try
{
	using var scope = app.Services.CreateScope();
	var database = scope.ServiceProvider.GetRequiredService<SocialDatabase>();
	await database.EnsureCreatedAsync();
}
catch (Exception ex)
{
	Console.WriteLine("Database setup failed.");
	Console.WriteLine(ex.Message);
	throw;
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/users", async (SocialDatabase database, CreateUserRequest request) =>
{
	var username = (request.Username ?? string.Empty).Trim().ToLowerInvariant();
	var displayName = (request.DisplayName ?? string.Empty).Trim();

	if (string.IsNullOrWhiteSpace(username))
	{
		return Results.BadRequest(new { error = "Username is required." });
	}

	await using var connection = database.CreateConnection();
	await connection.OpenAsync();

	await using (var existsCommand = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE username = @username", connection))
	{
		existsCommand.Parameters.AddWithValue("username", username);
		var count = Convert.ToInt32(await existsCommand.ExecuteScalarAsync());
		if (count > 0)
		{
			return Results.Conflict(new { error = "Username already exists." });
		}
	}

	var user = new SocialUser
	{
		Username = username,
		DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName
	};

	await using (var insertCommand = new NpgsqlCommand(
		"INSERT INTO users (id, username, display_name, created_at_utc) VALUES (@id, @username, @displayName, @createdAtUtc)",
		connection))
	{
		insertCommand.Parameters.AddWithValue("id", user.Id);
		insertCommand.Parameters.AddWithValue("username", user.Username);
		insertCommand.Parameters.AddWithValue("displayName", user.DisplayName);
		insertCommand.Parameters.AddWithValue("createdAtUtc", user.CreatedAtUtc);
		await insertCommand.ExecuteNonQueryAsync();
	}

	return Results.Created($"/users/{user.Id}", user);
});

app.MapGet("/users", async (SocialDatabase database) =>
{
	var users = new List<SocialUser>();

	await using var connection = database.CreateConnection();
	await connection.OpenAsync();
	await using var command = new NpgsqlCommand(
		"SELECT id, username, display_name, created_at_utc FROM users ORDER BY username",
		connection);
	await using var reader = await command.ExecuteReaderAsync();

	while (await reader.ReadAsync())
	{
		users.Add(new SocialUser
		{
			Id = reader.GetGuid(0),
			Username = reader.GetString(1),
			DisplayName = reader.GetString(2),
			CreatedAtUtc = reader.GetDateTime(3)
		});
	}

	return Results.Ok(users);
});

app.MapGet("/users/{userId:guid}", async (SocialDatabase database, Guid userId) =>
{
	await using var connection = database.CreateConnection();
	await connection.OpenAsync();
	await using var command = new NpgsqlCommand(
		"SELECT id, username, display_name, created_at_utc FROM users WHERE id = @id",
		connection);
	command.Parameters.AddWithValue("id", userId);
	await using var reader = await command.ExecuteReaderAsync();

	if (!await reader.ReadAsync())
	{
		return Results.NotFound();
	}

	var user = new SocialUser
	{
		Id = reader.GetGuid(0),
		Username = reader.GetString(1),
		DisplayName = reader.GetString(2),
		CreatedAtUtc = reader.GetDateTime(3)
	};

	return Results.Ok(user);
});

app.MapPost("/friend-requests", async (SocialDatabase database, CreateFriendRequestRequest request) =>
{
	if (request.RequesterId == request.RecipientId)
	{
		return Results.BadRequest(new { error = "You cannot friend yourself." });
	}

	await using var connection = database.CreateConnection();
	await connection.OpenAsync();

	if (!await UserExistsAsync(connection, request.RequesterId) || !await UserExistsAsync(connection, request.RecipientId))
	{
		return Results.BadRequest(new { error = "Both users must exist." });
	}

	await using (var existingCommand = new NpgsqlCommand(
		@"SELECT COUNT(*) FROM friend_requests
		  WHERE (requester_id = @requesterId AND recipient_id = @recipientId)
		     OR (requester_id = @recipientId AND recipient_id = @requesterId)",
		connection))
	{
		existingCommand.Parameters.AddWithValue("requesterId", request.RequesterId);
		existingCommand.Parameters.AddWithValue("recipientId", request.RecipientId);
		var count = Convert.ToInt32(await existingCommand.ExecuteScalarAsync());
		if (count > 0)
		{
			return Results.Conflict(new { error = "A friendship or request already exists between these users." });
		}
	}

	var friendRequest = new FriendRequest
	{
		RequesterId = request.RequesterId,
		RecipientId = request.RecipientId
	};

	await using (var insertCommand = new NpgsqlCommand(
		@"INSERT INTO friend_requests
		  (id, requester_id, recipient_id, status, created_at_utc, responded_at_utc)
		  VALUES (@id, @requesterId, @recipientId, @status, @createdAtUtc, @respondedAtUtc)",
		connection))
	{
		insertCommand.Parameters.AddWithValue("id", friendRequest.Id);
		insertCommand.Parameters.AddWithValue("requesterId", friendRequest.RequesterId);
		insertCommand.Parameters.AddWithValue("recipientId", friendRequest.RecipientId);
		insertCommand.Parameters.AddWithValue("status", friendRequest.Status.ToString());
		insertCommand.Parameters.AddWithValue("createdAtUtc", friendRequest.CreatedAtUtc);
		insertCommand.Parameters.AddWithValue("respondedAtUtc", DBNull.Value);
		await insertCommand.ExecuteNonQueryAsync();
	}

	return Results.Created($"/friend-requests/{friendRequest.Id}", friendRequest);
});

app.MapPost("/friend-requests/{requestId:guid}/accept", async (SocialDatabase database, Guid requestId) =>
{
	await using var connection = database.CreateConnection();
	await connection.OpenAsync();

	var friendRequest = await GetFriendRequestAsync(connection, requestId);
	if (friendRequest == null)
	{
		return Results.NotFound();
	}

	if (friendRequest.Status != FriendRequestStatus.Pending)
	{
		return Results.BadRequest(new { error = "Only pending requests can be accepted." });
	}

	friendRequest.Status = FriendRequestStatus.Accepted;
	friendRequest.RespondedAtUtc = DateTime.UtcNow;

	await using (var updateCommand = new NpgsqlCommand(
		"UPDATE friend_requests SET status = @status, responded_at_utc = @respondedAtUtc WHERE id = @id",
		connection))
	{
		updateCommand.Parameters.AddWithValue("id", friendRequest.Id);
		updateCommand.Parameters.AddWithValue("status", friendRequest.Status.ToString());
		updateCommand.Parameters.AddWithValue("respondedAtUtc", friendRequest.RespondedAtUtc!.Value);
		await updateCommand.ExecuteNonQueryAsync();
	}

	return Results.Ok(friendRequest);
});

app.MapGet("/users/{userId:guid}/friends", async (SocialDatabase database, Guid userId) =>
{
	var friends = new List<SocialUser>();

	await using var connection = database.CreateConnection();
	await connection.OpenAsync();
	await using var command = new NpgsqlCommand(
		@"SELECT u.id, u.username, u.display_name, u.created_at_utc
		  FROM friend_requests fr
		  JOIN users u
		    ON u.id =
		        CASE
		            WHEN fr.requester_id = @userId THEN fr.recipient_id
		            ELSE fr.requester_id
		        END
		  WHERE fr.status = 'Accepted'
		    AND (fr.requester_id = @userId OR fr.recipient_id = @userId)
		  ORDER BY u.username",
		connection);
	command.Parameters.AddWithValue("userId", userId);
	await using var reader = await command.ExecuteReaderAsync();

	while (await reader.ReadAsync())
	{
		friends.Add(new SocialUser
		{
			Id = reader.GetGuid(0),
			Username = reader.GetString(1),
			DisplayName = reader.GetString(2),
			CreatedAtUtc = reader.GetDateTime(3)
		});
	}

	return Results.Ok(friends);
});

app.MapPost("/conversations/direct", async (SocialDatabase database, CreateDirectConversationRequest request) =>
{
	if (request.FirstUserId == request.SecondUserId)
	{
		return Results.BadRequest(new { error = "A direct conversation needs two different users." });
	}

	await using var connection = database.CreateConnection();
	await connection.OpenAsync();

	if (!await UserExistsAsync(connection, request.FirstUserId) || !await UserExistsAsync(connection, request.SecondUserId))
	{
		return Results.BadRequest(new { error = "Both users must exist." });
	}

	var existingConversationId = await FindDirectConversationIdAsync(connection, request.FirstUserId, request.SecondUserId);
	if (existingConversationId != Guid.Empty)
	{
		return Results.Ok(new { id = existingConversationId, kind = "Direct" });
	}

	var conversation = new Conversation();

	await using (var insertConversation = new NpgsqlCommand(
		"INSERT INTO conversations (id, kind, created_at_utc) VALUES (@id, @kind, @createdAtUtc)",
		connection))
	{
		insertConversation.Parameters.AddWithValue("id", conversation.Id);
		insertConversation.Parameters.AddWithValue("kind", conversation.Kind.ToString());
		insertConversation.Parameters.AddWithValue("createdAtUtc", conversation.CreatedAtUtc);
		await insertConversation.ExecuteNonQueryAsync();
	}

	foreach (var userId in new[] { request.FirstUserId, request.SecondUserId })
	{
		await using var insertMember = new NpgsqlCommand(
			"INSERT INTO conversation_members (conversation_id, user_id, joined_at_utc) VALUES (@conversationId, @userId, @joinedAtUtc)",
			connection);
		insertMember.Parameters.AddWithValue("conversationId", conversation.Id);
		insertMember.Parameters.AddWithValue("userId", userId);
		insertMember.Parameters.AddWithValue("joinedAtUtc", DateTime.UtcNow);
		await insertMember.ExecuteNonQueryAsync();
	}

	return Results.Created($"/conversations/{conversation.Id}", conversation);
});

app.MapGet("/users/{userId:guid}/conversations", async (SocialDatabase database, Guid userId) =>
{
	var conversations = new List<Conversation>();

	await using var connection = database.CreateConnection();
	await connection.OpenAsync();
	await using var command = new NpgsqlCommand(
		@"SELECT c.id, c.kind, c.created_at_utc
		  FROM conversations c
		  JOIN conversation_members cm ON cm.conversation_id = c.id
		  WHERE cm.user_id = @userId
		  ORDER BY c.created_at_utc DESC",
		connection);
	command.Parameters.AddWithValue("userId", userId);
	await using var reader = await command.ExecuteReaderAsync();

	while (await reader.ReadAsync())
	{
		conversations.Add(new Conversation
		{
			Id = reader.GetGuid(0),
			Kind = Enum.TryParse<ConversationKind>(reader.GetString(1), out var kind) ? kind : ConversationKind.Direct,
			CreatedAtUtc = reader.GetDateTime(2)
		});
	}

	return Results.Ok(conversations);
});

app.MapGet("/conversations/{conversationId:guid}/messages", async (SocialDatabase database, Guid conversationId) =>
{
	var messages = new List<SocialMessageRecord>();

	await using var connection = database.CreateConnection();
	await connection.OpenAsync();
	await using var command = new NpgsqlCommand(
		@"SELECT id, conversation_id, sender_id, body, media_url, media_type, created_at_utc
		  FROM messages
		  WHERE conversation_id = @conversationId
		  ORDER BY created_at_utc",
		connection);
	command.Parameters.AddWithValue("conversationId", conversationId);
	await using var reader = await command.ExecuteReaderAsync();

	while (await reader.ReadAsync())
	{
		messages.Add(new SocialMessageRecord
		{
			Id = reader.GetGuid(0),
			ConversationId = reader.GetGuid(1),
			SenderId = reader.GetGuid(2),
			Body = reader.GetString(3),
			MediaUrl = reader.GetString(4),
			MediaType = reader.GetString(5),
			CreatedAtUtc = reader.GetDateTime(6)
		});
	}

	return Results.Ok(messages);
});

app.MapPost("/messages", async (SocialDatabase database, CreateMessageRequest request) =>
{
	var body = (request.Body ?? string.Empty).Trim();
	var mediaUrl = (request.MediaUrl ?? string.Empty).Trim();

	if (string.IsNullOrWhiteSpace(body) && string.IsNullOrWhiteSpace(mediaUrl))
	{
		return Results.BadRequest(new { error = "A message needs text or media." });
	}

	await using var connection = database.CreateConnection();
	await connection.OpenAsync();

	if (!await ConversationExistsAsync(connection, request.ConversationId))
	{
		return Results.BadRequest(new { error = "Conversation not found." });
	}

	if (!await ConversationHasUserAsync(connection, request.ConversationId, request.SenderId))
	{
		return Results.BadRequest(new { error = "Sender is not a member of this conversation." });
	}

	var message = new SocialMessageRecord
	{
		ConversationId = request.ConversationId,
		SenderId = request.SenderId,
		Body = body,
		MediaUrl = mediaUrl,
		MediaType = string.IsNullOrWhiteSpace(request.MediaType) ? string.Empty : request.MediaType.Trim()
	};

	await using var command = new NpgsqlCommand(
		@"INSERT INTO messages
		  (id, conversation_id, sender_id, body, media_url, media_type, created_at_utc)
		  VALUES (@id, @conversationId, @senderId, @body, @mediaUrl, @mediaType, @createdAtUtc)",
		connection);
	command.Parameters.AddWithValue("id", message.Id);
	command.Parameters.AddWithValue("conversationId", message.ConversationId);
	command.Parameters.AddWithValue("senderId", message.SenderId);
	command.Parameters.AddWithValue("body", message.Body);
	command.Parameters.AddWithValue("mediaUrl", message.MediaUrl);
	command.Parameters.AddWithValue("mediaType", message.MediaType);
	command.Parameters.AddWithValue("createdAtUtc", message.CreatedAtUtc);
	await command.ExecuteNonQueryAsync();

	return Results.Created($"/conversations/{message.ConversationId}/messages/{message.Id}", message);
});

app.MapPost("/posts", async (SocialDatabase database, CreatePostRequest request) =>
{
	var mediaUrl = (request.MediaUrl ?? string.Empty).Trim();
	if (string.IsNullOrWhiteSpace(mediaUrl))
	{
		return Results.BadRequest(new { error = "MediaUrl is required." });
	}

	await using var connection = database.CreateConnection();
	await connection.OpenAsync();

	if (!await UserExistsAsync(connection, request.AuthorId))
	{
		return Results.BadRequest(new { error = "Author not found." });
	}

	var post = new SocialPost
	{
		AuthorId = request.AuthorId,
		Caption = string.IsNullOrWhiteSpace(request.Caption) ? string.Empty : request.Caption.Trim(),
		MediaUrl = mediaUrl
	};

	await using var command = new NpgsqlCommand(
		"INSERT INTO posts (id, author_id, caption, media_url, created_at_utc) VALUES (@id, @authorId, @caption, @mediaUrl, @createdAtUtc)",
		connection);
	command.Parameters.AddWithValue("id", post.Id);
	command.Parameters.AddWithValue("authorId", post.AuthorId);
	command.Parameters.AddWithValue("caption", post.Caption);
	command.Parameters.AddWithValue("mediaUrl", post.MediaUrl);
	command.Parameters.AddWithValue("createdAtUtc", post.CreatedAtUtc);
	await command.ExecuteNonQueryAsync();

	return Results.Created($"/posts/{post.Id}", post);
});

app.MapGet("/users/{userId:guid}/feed", async (SocialDatabase database, Guid userId) =>
{
	var posts = new List<SocialPost>();

	await using var connection = database.CreateConnection();
	await connection.OpenAsync();
	await using var command = new NpgsqlCommand(
		@"SELECT p.id, p.author_id, p.caption, p.media_url, p.created_at_utc
		  FROM posts p
		  WHERE p.author_id = @userId
		     OR p.author_id IN
		        (
		            SELECT CASE
		                WHEN requester_id = @userId THEN recipient_id
		                ELSE requester_id
		            END
		            FROM friend_requests
		            WHERE status = 'Accepted'
		              AND (requester_id = @userId OR recipient_id = @userId)
		        )
		  ORDER BY p.created_at_utc DESC",
		connection);
	command.Parameters.AddWithValue("userId", userId);
	await using var reader = await command.ExecuteReaderAsync();

	while (await reader.ReadAsync())
	{
		posts.Add(new SocialPost
		{
			Id = reader.GetGuid(0),
			AuthorId = reader.GetGuid(1),
			Caption = reader.GetString(2),
			MediaUrl = reader.GetString(3),
			CreatedAtUtc = reader.GetDateTime(4)
		});
	}

	return Results.Ok(posts);
});

app.Run();

static async Task<bool> UserExistsAsync(NpgsqlConnection connection, Guid userId)
{
	await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE id = @id", connection);
	command.Parameters.AddWithValue("id", userId);
	var count = Convert.ToInt32(await command.ExecuteScalarAsync());
	return count > 0;
}

static async Task<bool> ConversationExistsAsync(NpgsqlConnection connection, Guid conversationId)
{
	await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM conversations WHERE id = @id", connection);
	command.Parameters.AddWithValue("id", conversationId);
	var count = Convert.ToInt32(await command.ExecuteScalarAsync());
	return count > 0;
}

static async Task<bool> ConversationHasUserAsync(NpgsqlConnection connection, Guid conversationId, Guid userId)
{
	await using var command = new NpgsqlCommand(
		"SELECT COUNT(*) FROM conversation_members WHERE conversation_id = @conversationId AND user_id = @userId",
		connection);
	command.Parameters.AddWithValue("conversationId", conversationId);
	command.Parameters.AddWithValue("userId", userId);
	var count = Convert.ToInt32(await command.ExecuteScalarAsync());
	return count > 0;
}

static async Task<Guid> FindDirectConversationIdAsync(NpgsqlConnection connection, Guid firstUserId, Guid secondUserId)
{
	await using var command = new NpgsqlCommand(
		@"SELECT c.id
		  FROM conversations c
		  JOIN conversation_members cm ON cm.conversation_id = c.id
		  WHERE c.kind = 'Direct'
		    AND cm.user_id IN (@firstUserId, @secondUserId)
		  GROUP BY c.id
		  HAVING COUNT(*) = 2",
		connection);
	command.Parameters.AddWithValue("firstUserId", firstUserId);
	command.Parameters.AddWithValue("secondUserId", secondUserId);

	var result = await command.ExecuteScalarAsync();
	if (result == null || result == DBNull.Value)
	{
		return Guid.Empty;
	}

	return (Guid)result;
}

static async Task<FriendRequest?> GetFriendRequestAsync(NpgsqlConnection connection, Guid requestId)
{
	await using var command = new NpgsqlCommand(
		@"SELECT id, requester_id, recipient_id, status, created_at_utc, responded_at_utc
		  FROM friend_requests
		  WHERE id = @id",
		connection);
	command.Parameters.AddWithValue("id", requestId);
	await using var reader = await command.ExecuteReaderAsync();

	if (!await reader.ReadAsync())
	{
		return null;
	}

	return new FriendRequest
	{
		Id = reader.GetGuid(0),
		RequesterId = reader.GetGuid(1),
		RecipientId = reader.GetGuid(2),
		Status = Enum.TryParse<FriendRequestStatus>(reader.GetString(3), out var status) ? status : FriendRequestStatus.Pending,
		CreatedAtUtc = reader.GetDateTime(4),
		RespondedAtUtc = reader.IsDBNull(5) ? null : reader.GetDateTime(5)
	};
}

internal sealed record CreateUserRequest(string Username, string? DisplayName);
internal sealed record CreateFriendRequestRequest(Guid RequesterId, Guid RecipientId);
internal sealed record CreateDirectConversationRequest(Guid FirstUserId, Guid SecondUserId);
internal sealed record CreateMessageRequest(Guid ConversationId, Guid SenderId, string? Body, string? MediaUrl, string? MediaType);
internal sealed record CreatePostRequest(Guid AuthorId, string? Caption, string MediaUrl);
