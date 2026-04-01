namespace inplayed;

internal sealed class SocialService
{
	private readonly SocialApiClient _apiClient = new();

	public bool IsLoggedIn => SocialAuthSession.IsLoggedIn;
	public string CurrentUsername => SocialAuthSession.Username;
	public string CurrentDisplayName => SocialAuthSession.DisplayName;

	public bool CreateAccount(string username, string displayName, string password, out string message)
	{
		return _apiClient.CreateAccount(username, displayName, password, out message);
	}

	public bool Login(string username, string password, out string message)
	{
		return _apiClient.Login(username, password, out message);
	}

	public void Logout()
	{
		_apiClient.Logout();
	}

	public IReadOnlyList<string> GetFriends()
	{
		if (!IsLoggedIn)
		{
			return [];
		}

		return _apiClient.GetFriends(out _);
	}

	public IReadOnlyList<SocialFriendRequest> GetPendingFriendRequests()
	{
		if (!IsLoggedIn)
		{
			return [];
		}

		return _apiClient.GetPendingFriendRequests(out _);
	}

	public bool SendFriendRequest(string username, out string normalizedUsername, out string message)
	{
		normalizedUsername = string.Empty;
		if (!IsLoggedIn)
		{
			message = "Log in first.";
			return false;
		}

		return _apiClient.SendFriendRequest(username, out normalizedUsername, out message);
	}

	public bool AcceptFriendRequest(int requestId, out string message)
	{
		if (!IsLoggedIn)
		{
			message = "Log in first.";
			return false;
		}

		return _apiClient.AcceptFriendRequest(requestId, out message);
	}

	public bool RejectFriendRequest(int requestId, out string message)
	{
		if (!IsLoggedIn)
		{
			message = "Log in first.";
			return false;
		}

		return _apiClient.RejectFriendRequest(requestId, out message);
	}

	public bool RemoveFriend(string friendName)
	{
		if (!IsLoggedIn)
		{
			return false;
		}

		return _apiClient.RemoveFriend(friendName, out _);
	}

	public IReadOnlyList<SocialMessage> GetMessages(string friendName)
	{
		if (!IsLoggedIn)
		{
			return [];
		}

		return _apiClient.GetMessages(friendName, out _);
	}

	public SocialMessage? SendTextMessage(string friendName, string rawMessage)
	{
		if (!IsLoggedIn)
		{
			return null;
		}

		var messageBody = rawMessage.Trim();
		if (string.IsNullOrWhiteSpace(messageBody))
		{
			return null;
		}

		return _apiClient.SendTextMessage(friendName, messageBody, out _);
	}

	public SocialMessage? ShareClip(string friendName, string clipPath, string caption)
	{
		if (!IsLoggedIn)
		{
			return null;
		}

		if (string.IsNullOrWhiteSpace(clipPath))
		{
			return null;
		}

		return _apiClient.ShareClip(friendName, clipPath, caption, out _);
	}
}
