# inplayed Server

This is a self-hosted ASP.NET Core backend for social features.

It currently supports:
- users
- friend requests
- friends list
- direct conversations
- messages
- social posts
- a basic feed

## Database

The server uses PostgreSQL.

The table setup is in:
- `sql/schema.sql`

The schema uses:
- integer IDs
- `VARCHAR(...)` for text fields with clear size limits
- `TIMESTAMP` for date and time values

Default connection string location:
- `appsettings.json`
- or `ConnectionStrings__SocialDatabase` environment variable

## Run

```powershell
dotnet run --project server\server.csproj
```

## Main endpoints

- `GET /health`
- `POST /users`
- `GET /users`
- `GET /users/{userId}`
- `POST /friend-requests`
- `POST /friend-requests/{requestId}/accept`
- `GET /users/{userId}/friends`
- `POST /conversations/direct`
- `GET /users/{userId}/conversations`
- `GET /conversations/{conversationId}/messages`
- `POST /messages`
- `POST /posts`
- `GET /users/{userId}/feed`
