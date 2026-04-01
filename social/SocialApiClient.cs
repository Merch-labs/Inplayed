using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace inplayed;

internal sealed class SocialApiClient
{
	private static readonly HttpClient Http = new()
	{
		Timeout = TimeSpan.FromSeconds(8)
	};

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public bool CreateAccount(string username, string displayName, string password, out string message)
	{
		message = string.Empty;
		var request = new RegisterRequest
		{
			Username = username.Trim(),
			DisplayName = displayName.Trim(),
			Password = password
		};

		var result = Post<AuthResponse>("/api/auth/register", request, includeSessionToken: false, out message);
		if (result == null)
		{
			return false;
		}

		SocialAuthSession.Set(result.SessionToken, result.Username, result.DisplayName);
		message = $"Created account for @{result.Username}";
		return true;
	}

	public bool Login(string username, string password, out string message)
	{
		message = string.Empty;
		var request = new LoginRequest
		{
			Username = username.Trim(),
			Password = password
		};

		var result = Post<AuthResponse>("/api/auth/login", request, includeSessionToken: false, out message);
		if (result == null)
		{
			return false;
		}

		SocialAuthSession.Set(result.SessionToken, result.Username, result.DisplayName);
		message = $"Logged in as @{result.Username}";
		return true;
	}

	public void Logout()
	{
		Post<object>("/api/auth/logout", new { }, includeSessionToken: true, out _);
		SocialAuthSession.Clear();
	}

	public IReadOnlyList<string> GetFriends(out string message)
	{
		message = string.Empty;
		var response = Get<List<FriendDto>>("/api/friends", includeSessionToken: true, out message);
		if (response == null)
		{
			return [];
		}

		var friends = new List<string>();
		foreach (var friend in response)
		{
			if (!string.IsNullOrWhiteSpace(friend.Username))
			{
				friends.Add(friend.Username);
			}
		}

		return friends;
	}

	public IReadOnlyList<SocialFriendRequest> GetPendingFriendRequests(out string message)
	{
		message = string.Empty;
		var response = Get<List<SocialFriendRequest>>("/api/friend-requests/pending", includeSessionToken: true, out message);
		if (response == null)
		{
			return [];
		}

		return response;
	}

	public bool SendFriendRequest(string username, out string normalizedUsername, out string message)
	{
		normalizedUsername = string.Empty;
		message = string.Empty;

		var request = new SendFriendRequestRequest
		{
			Username = username.Trim()
		};

		var response = Post<FriendRequestSendResponse>("/api/friend-requests", request, includeSessionToken: true, out message);
		if (response == null)
		{
			return false;
		}

		normalizedUsername = response.Username;
		message = response.Message;
		return true;
	}

	public bool AcceptFriendRequest(int requestId, out string message)
	{
		var response = Post<MessageResponse>($"/api/friend-requests/{requestId}/accept", new { }, includeSessionToken: true, out message);
		if (response == null)
		{
			return false;
		}

		message = response.Message;
		return true;
	}

	public bool RejectFriendRequest(int requestId, out string message)
	{
		var response = Post<MessageResponse>($"/api/friend-requests/{requestId}/reject", new { }, includeSessionToken: true, out message);
		if (response == null)
		{
			return false;
		}

		message = response.Message;
		return true;
	}

	public bool RemoveFriend(string username, out string message)
	{
		var response = Delete<MessageResponse>($"/api/friends/{Uri.EscapeDataString(username.Trim())}", includeSessionToken: true, out message);
		if (response == null)
		{
			return false;
		}

		message = response.Message;
		return true;
	}

	public IReadOnlyList<SocialMessage> GetMessages(string friendUsername, out string message)
	{
		message = string.Empty;
		var response = Get<List<SocialMessage>>($"/api/messages/{Uri.EscapeDataString(friendUsername.Trim())}", includeSessionToken: true, out message);
		if (response == null)
		{
			return [];
		}

		return response;
	}

	public SocialMessage? SendTextMessage(string friendUsername, string body, out string message)
	{
		message = string.Empty;
		var request = new SendMessageRequest
		{
			Kind = SocialMessageKinds.Text,
			Body = body.Trim(),
			ClipPath = string.Empty,
			ClipFileName = string.Empty
		};

		return Post<SocialMessage>($"/api/messages/{Uri.EscapeDataString(friendUsername.Trim())}", request, includeSessionToken: true, out message);
	}

	public SocialMessage? ShareClip(string friendUsername, string clipPath, string caption, out string message)
	{
		message = string.Empty;
		var request = new SendMessageRequest
		{
			Kind = SocialMessageKinds.Clip,
			Body = caption.Trim(),
			ClipPath = clipPath,
			ClipFileName = Path.GetFileName(clipPath)
		};

		return Post<SocialMessage>($"/api/messages/{Uri.EscapeDataString(friendUsername.Trim())}", request, includeSessionToken: true, out message);
	}

	private static T? Get<T>(string path, bool includeSessionToken, out string message)
	{
		return Send<T>(HttpMethod.Get, path, null, includeSessionToken, out message);
	}

	private static T? Delete<T>(string path, bool includeSessionToken, out string message)
	{
		return Send<T>(HttpMethod.Delete, path, null, includeSessionToken, out message);
	}

	private static T? Post<T>(string path, object payload, bool includeSessionToken, out string message)
	{
		return Send<T>(HttpMethod.Post, path, payload, includeSessionToken, out message);
	}

	private static T? Send<T>(HttpMethod method, string path, object? payload, bool includeSessionToken, out string message)
	{
		message = string.Empty;
		try
		{
			using var request = new HttpRequestMessage(method, SocialServerSettings.GetBaseUrl() + path);
			if (payload != null)
			{
				request.Content = JsonContent.Create(payload);
			}

			if (includeSessionToken && SocialAuthSession.IsLoggedIn)
			{
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", SocialAuthSession.SessionToken);
			}

			using var response = Http.Send(request);
			if (!response.IsSuccessStatusCode)
			{
				message = ReadErrorMessage(response);
				return default;
			}

			if (typeof(T) == typeof(object))
			{
				return (T)(object)new object();
			}

			var result = response.Content.ReadFromJsonAsync<T>(JsonOptions).GetAwaiter().GetResult();
			if (result == null)
			{
				message = "The server returned an empty response.";
				return default;
			}

			return result;
		}
		catch (Exception ex)
		{
			message = $"Could not reach the social server at {SocialServerSettings.GetBaseUrl()}. {ex.Message}";
			return default;
		}
	}

	private static string ReadErrorMessage(HttpResponseMessage response)
	{
		try
		{
			var error = response.Content.ReadFromJsonAsync<MessageResponse>(JsonOptions).GetAwaiter().GetResult();
			if (error != null && !string.IsNullOrWhiteSpace(error.Message))
			{
				return error.Message;
			}
		}
		catch
		{
		}

		return $"The server returned {(int)response.StatusCode} {response.ReasonPhrase}.";
	}

	private sealed class RegisterRequest
	{
		public string Username { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
	}

	private sealed class LoginRequest
	{
		public string Username { get; set; } = string.Empty;
		public string Password { get; set; } = string.Empty;
	}

	private sealed class SendFriendRequestRequest
	{
		public string Username { get; set; } = string.Empty;
	}

	private sealed class SendMessageRequest
	{
		public string Kind { get; set; } = string.Empty;
		public string Body { get; set; } = string.Empty;
		public string ClipPath { get; set; } = string.Empty;
		public string ClipFileName { get; set; } = string.Empty;
	}

	private sealed class AuthResponse
	{
		public string SessionToken { get; set; } = string.Empty;
		public string Username { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
	}

	private sealed class FriendDto
	{
		public string Username { get; set; } = string.Empty;
	}

	private sealed class FriendRequestSendResponse
	{
		public string Username { get; set; } = string.Empty;
		public string Message { get; set; } = string.Empty;
	}

	private sealed class MessageResponse
	{
		public string Message { get; set; } = string.Empty;
	}
}
