using inplayed.Server.Data;
using inplayed.Server.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SocialDbContext>(options =>
{
	options.UseNpgsql(builder.Configuration.GetConnectionString("SocialDatabase"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
	var db = scope.ServiceProvider.GetRequiredService<SocialDbContext>();
	await db.Database.EnsureCreatedAsync();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/users", async (SocialDbContext db, CreateUserRequest request) =>
{
	var username = (request.Username ?? string.Empty).Trim().ToLowerInvariant();
	var displayName = (request.DisplayName ?? string.Empty).Trim();

	if (string.IsNullOrWhiteSpace(username))
	{
		return Results.BadRequest(new { error = "Username is required." });
	}

	var exists = await db.Users.AnyAsync(user => user.Username == username);
	if (exists)
	{
		return Results.Conflict(new { error = "Username already exists." });
	}

	var user = new SocialUser
	{
		Username = username,
		DisplayName = string.IsNullOrWhiteSpace(displayName) ? username : displayName
	};

	db.Users.Add(user);
	await db.SaveChangesAsync();
	return Results.Created($"/users/{user.Id}", user);
});

app.MapGet("/users", async (SocialDbContext db) =>
{
	var users = await db.Users
		.OrderBy(user => user.Username)
		.ToListAsync();

	return Results.Ok(users);
});

app.MapGet("/users/{userId:guid}", async (SocialDbContext db, Guid userId) =>
{
	var user = await db.Users.FindAsync(userId);
	return user == null ? Results.NotFound() : Results.Ok(user);
});

app.MapPost("/friend-requests", async (SocialDbContext db, CreateFriendRequestRequest request) =>
{
	if (request.RequesterId == request.RecipientId)
	{
		return Results.BadRequest(new { error = "You cannot friend yourself." });
	}

	var requester = await db.Users.FindAsync(request.RequesterId);
	var recipient = await db.Users.FindAsync(request.RecipientId);
	if (requester == null || recipient == null)
	{
		return Results.BadRequest(new { error = "Both users must exist." });
	}

	var existing = await db.FriendRequests.FirstOrDefaultAsync(friendRequest =>
		(friendRequest.RequesterId == request.RequesterId && friendRequest.RecipientId == request.RecipientId) ||
		(friendRequest.RequesterId == request.RecipientId && friendRequest.RecipientId == request.RequesterId));

	if (existing != null)
	{
		return Results.Conflict(new { error = "A friendship or request already exists between these users." });
	}

	var friendRequest = new FriendRequest
	{
		RequesterId = request.RequesterId,
		RecipientId = request.RecipientId
	};

	db.FriendRequests.Add(friendRequest);
	await db.SaveChangesAsync();
	return Results.Created($"/friend-requests/{friendRequest.Id}", friendRequest);
});

app.MapPost("/friend-requests/{requestId:guid}/accept", async (SocialDbContext db, Guid requestId) =>
{
	var friendRequest = await db.FriendRequests.FindAsync(requestId);
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
	await db.SaveChangesAsync();
	return Results.Ok(friendRequest);
});

app.MapGet("/users/{userId:guid}/friends", async (SocialDbContext db, Guid userId) =>
{
	var acceptedRequests = await db.FriendRequests
		.Where(friendRequest =>
			friendRequest.Status == FriendRequestStatus.Accepted &&
			(friendRequest.RequesterId == userId || friendRequest.RecipientId == userId))
		.Include(friendRequest => friendRequest.Requester)
		.Include(friendRequest => friendRequest.Recipient)
		.ToListAsync();

	var friends = new List<SocialUser>();
	foreach (var friendRequest in acceptedRequests)
	{
		if (friendRequest.RequesterId == userId && friendRequest.Recipient != null)
		{
			friends.Add(friendRequest.Recipient);
		}
		else if (friendRequest.Requester != null)
		{
			friends.Add(friendRequest.Requester);
		}
	}

	return Results.Ok(friends);
});

app.MapPost("/conversations/direct", async (SocialDbContext db, CreateDirectConversationRequest request) =>
{
	if (request.FirstUserId == request.SecondUserId)
	{
		return Results.BadRequest(new { error = "A direct conversation needs two different users." });
	}

	var firstUser = await db.Users.FindAsync(request.FirstUserId);
	var secondUser = await db.Users.FindAsync(request.SecondUserId);
	if (firstUser == null || secondUser == null)
	{
		return Results.BadRequest(new { error = "Both users must exist." });
	}

	var existingConversation = await db.Conversations
		.Include(conversation => conversation.Members)
		.FirstOrDefaultAsync(conversation =>
			conversation.Kind == ConversationKind.Direct &&
			conversation.Members.Count == 2 &&
			conversation.Members.Any(member => member.UserId == request.FirstUserId) &&
			conversation.Members.Any(member => member.UserId == request.SecondUserId));

	if (existingConversation != null)
	{
		return Results.Ok(existingConversation);
	}

	var conversation = new Conversation
	{
		Kind = ConversationKind.Direct,
		Members =
		[
			new ConversationMember { UserId = request.FirstUserId },
			new ConversationMember { UserId = request.SecondUserId }
		]
	};

	db.Conversations.Add(conversation);
	await db.SaveChangesAsync();
	return Results.Created($"/conversations/{conversation.Id}", conversation);
});

app.MapGet("/users/{userId:guid}/conversations", async (SocialDbContext db, Guid userId) =>
{
	var conversations = await db.Conversations
		.Include(conversation => conversation.Members)
		.Where(conversation => conversation.Members.Any(member => member.UserId == userId))
		.OrderByDescending(conversation => conversation.CreatedAtUtc)
		.ToListAsync();

	return Results.Ok(conversations);
});

app.MapGet("/conversations/{conversationId:guid}/messages", async (SocialDbContext db, Guid conversationId) =>
{
	var messages = await db.Messages
		.Where(message => message.ConversationId == conversationId)
		.OrderBy(message => message.CreatedAtUtc)
		.ToListAsync();

	return Results.Ok(messages);
});

app.MapPost("/messages", async (SocialDbContext db, CreateMessageRequest request) =>
{
	var conversation = await db.Conversations
		.Include(item => item.Members)
		.FirstOrDefaultAsync(item => item.Id == request.ConversationId);

	if (conversation == null)
	{
		return Results.BadRequest(new { error = "Conversation not found." });
	}

	var senderIsMember = conversation.Members.Any(member => member.UserId == request.SenderId);
	if (!senderIsMember)
	{
		return Results.BadRequest(new { error = "Sender is not a member of this conversation." });
	}

	var body = (request.Body ?? string.Empty).Trim();
	var mediaUrl = (request.MediaUrl ?? string.Empty).Trim();
	if (string.IsNullOrWhiteSpace(body) && string.IsNullOrWhiteSpace(mediaUrl))
	{
		return Results.BadRequest(new { error = "A message needs text or media." });
	}

	var message = new SocialMessageRecord
	{
		ConversationId = request.ConversationId,
		SenderId = request.SenderId,
		Body = body,
		MediaUrl = mediaUrl,
		MediaType = string.IsNullOrWhiteSpace(request.MediaType) ? string.Empty : request.MediaType.Trim()
	};

	db.Messages.Add(message);
	await db.SaveChangesAsync();
	return Results.Created($"/conversations/{message.ConversationId}/messages/{message.Id}", message);
});

app.MapPost("/posts", async (SocialDbContext db, CreatePostRequest request) =>
{
	var user = await db.Users.FindAsync(request.AuthorId);
	if (user == null)
	{
		return Results.BadRequest(new { error = "Author not found." });
	}

	var mediaUrl = (request.MediaUrl ?? string.Empty).Trim();
	if (string.IsNullOrWhiteSpace(mediaUrl))
	{
		return Results.BadRequest(new { error = "MediaUrl is required." });
	}

	var post = new SocialPost
	{
		AuthorId = request.AuthorId,
		Caption = string.IsNullOrWhiteSpace(request.Caption) ? string.Empty : request.Caption.Trim(),
		MediaUrl = mediaUrl
	};

	db.Posts.Add(post);
	await db.SaveChangesAsync();
	return Results.Created($"/posts/{post.Id}", post);
});

app.MapGet("/users/{userId:guid}/feed", async (SocialDbContext db, Guid userId) =>
{
	var acceptedRequests = await db.FriendRequests
		.Where(friendRequest =>
			friendRequest.Status == FriendRequestStatus.Accepted &&
			(friendRequest.RequesterId == userId || friendRequest.RecipientId == userId))
		.ToListAsync();

	var allowedAuthorIds = new HashSet<Guid> { userId };
	foreach (var friendRequest in acceptedRequests)
	{
		if (friendRequest.RequesterId == userId)
		{
			allowedAuthorIds.Add(friendRequest.RecipientId);
		}
		else
		{
			allowedAuthorIds.Add(friendRequest.RequesterId);
		}
	}

	var posts = await db.Posts
		.Where(post => allowedAuthorIds.Contains(post.AuthorId))
		.OrderByDescending(post => post.CreatedAtUtc)
		.ToListAsync();

	return Results.Ok(posts);
});

app.Run();

internal sealed record CreateUserRequest(string Username, string? DisplayName);
internal sealed record CreateFriendRequestRequest(Guid RequesterId, Guid RecipientId);
internal sealed record CreateDirectConversationRequest(Guid FirstUserId, Guid SecondUserId);
internal sealed record CreateMessageRequest(Guid ConversationId, Guid SenderId, string? Body, string? MediaUrl, string? MediaType);
internal sealed record CreatePostRequest(Guid AuthorId, string? Caption, string MediaUrl);
