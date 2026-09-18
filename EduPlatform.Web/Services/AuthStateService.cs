using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;

namespace EduPlatform.Web.Services;

public class AuthStateService
{
    private const string StorageKey = "eduplatform-auth";
    private readonly IJSRuntime _js;
    private readonly IHttpClientFactory _httpClientFactory;

    public bool IsAuthenticated { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string RefreshToken { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public Guid SessionId { get; private set; }
    public bool RememberMe { get; private set; } = true;

    public event Action? OnChange;

    private readonly TaskCompletionSource _initializationTcs = new();

    public AuthStateService(IJSRuntime js, IHttpClientFactory httpClientFactory)
    {
        _js = js;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Waits until the first auth-restore attempt has completed (success or failure).
    /// Does not guarantee the final auth state: subscribe to <see cref="OnChange"/> to react
    /// to later updates (e.g. once the interactive circuit re-attaches after prerendering).
    /// </summary>
    public Task WaitForInitializationAsync() => _initializationTcs.Task;

    public async Task InitializeAsync()
    {
        try
        {
            try
            {
                // "Remember me" sessions live in localStorage; tab-only sessions live in sessionStorage.
                var json = await _js.InvokeAsync<string?>("sessionStorage.getItem", StorageKey)
                    ?? await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);

                if (!string.IsNullOrWhiteSpace(json))
                {
                    var session = JsonSerializer.Deserialize<AuthSession>(json);
                    if (session is not null)
                    {
                        session.SessionId = await GetOrCreateSessionIdAsync(session.SessionId);

                        if (IsTokenExpired(session.Token))
                        {
                            if (!string.IsNullOrWhiteSpace(session.RefreshToken)
                                && await TryRefreshAsync(session))
                            {
                                return;
                            }

                            await LogoutAsync();
                            return;
                        }

                        ApplySession(session);
                        return;
                    }
                }
            }
            catch
            {
                // Ignore in prerender or unsupported context.
            }

            ClearSession();
        }
        finally
        {
            _initializationTcs.TrySetResult();
        }
    }

    public async Task LoginAsync(
        string token, string userId, string firstName, string email, string role,
        string refreshToken = "", bool rememberMe = true)
    {
        if (IsTokenExpired(token))
        {
            await LogoutAsync();
            return;
        }

        var session = new AuthSession
        {
            Token = token,
            RefreshToken = refreshToken,
            UserId = userId,
            FirstName = firstName,
            Email = email,
            Role = role,
            RememberMe = rememberMe,
            SessionId = await GetOrCreateSessionIdAsync(Guid.Empty)
        };

        await SaveSessionAsync(session);
        ApplySession(session);
    }

    public async Task UpdateFirstNameAsync(string firstName)
    {
        var session = new AuthSession
        {
            Token = Token,
            RefreshToken = RefreshToken,
            UserId = UserId,
            FirstName = firstName,
            Email = Email,
            Role = Role,
            SessionId = SessionId,
            RememberMe = RememberMe
        };

        await SaveSessionAsync(session);
        ApplySession(session);
    }

    public async Task LogoutAsync()
    {
        var refreshToken = RefreshToken;

        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            await _js.InvokeVoidAsync("sessionStorage.removeItem", StorageKey);
            await _js.InvokeVoidAsync("eduPlatformSession.clear");
        }
        catch
        {
            // Ignore if browser storage is unavailable.
        }

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            try
            {
                var client = _httpClientFactory.CreateClient("EduPlatformAPI");
                await client.PostAsJsonAsync("api/auth/logout", new { refreshToken });
            }
            catch
            {
                // Best-effort server-side revocation; local session is cleared regardless.
            }
        }

        ClearSession();
    }

    /// <summary>
    /// Attempts to silently exchange the stored refresh token for a new access token.
    /// Returns false (and leaves the caller to log out) if the refresh token is missing/invalid.
    /// </summary>
    public async Task<bool> TryRefreshAsync()
    {
        var current = new AuthSession
        {
            Token = Token,
            RefreshToken = RefreshToken,
            UserId = UserId,
            FirstName = FirstName,
            Email = Email,
            Role = Role,
            SessionId = SessionId,
            RememberMe = true
        };

        return await TryRefreshAsync(current);
    }

    private async Task<bool> TryRefreshAsync(AuthSession session)
    {
        if (string.IsNullOrWhiteSpace(session.RefreshToken))
            return false;

        try
        {
            var client = _httpClientFactory.CreateClient("EduPlatformAPI");
            var response = await client.PostAsJsonAsync("api/auth/refresh", new { refreshToken = session.RefreshToken });
            if (!response.IsSuccessStatusCode)
                return false;

            var result = await response.Content.ReadFromJsonAsync<RefreshResponse>();
            if (result is null || string.IsNullOrWhiteSpace(result.Token))
                return false;

            var refreshed = new AuthSession
            {
                Token = result.Token,
                RefreshToken = result.RefreshToken,
                UserId = result.UserId.ToString(),
                FirstName = result.FirstName,
                Email = session.Email,
                Role = RoleToString(result.Role),
                RememberMe = session.RememberMe,
                SessionId = session.SessionId
            };

            await SaveSessionAsync(refreshed);
            ApplySession(refreshed);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string RoleToString(int role) => role switch
    {
        1 => "Instructor",
        2 => "Admin",
        _ => "Student"
    };

    private async Task SaveSessionAsync(AuthSession session)
    {
        try
        {
            var json = JsonSerializer.Serialize(session);
            var storage = session.RememberMe ? "localStorage" : "sessionStorage";
            await _js.InvokeVoidAsync($"{storage}.setItem", StorageKey, json);
        }
        catch
        {
            // Ignore if browser storage is unavailable.
        }
    }

    private async Task<Guid> GetOrCreateSessionIdAsync(Guid currentSessionId)
    {
        try
        {
            var cookieValue = await _js.InvokeAsync<string>("eduPlatformSession.get");
            if (Guid.TryParse(cookieValue, out var cookieSessionId))
                return cookieSessionId;

            var sessionId = currentSessionId == Guid.Empty ? Guid.NewGuid() : currentSessionId;
            await _js.InvokeVoidAsync("eduPlatformSession.set", sessionId.ToString());
            return sessionId;
        }
        catch
        {
            return currentSessionId == Guid.Empty ? Guid.NewGuid() : currentSessionId;
        }
    }

    public bool IsTokenExpired(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return true;

        try
        {
            var payload = token.Split('.')[1];
            var normalized = payload.Replace('-', '+').Replace('_', '/');
            var padded = normalized.PadRight(normalized.Length + ((4 - normalized.Length % 4) % 4), '=');

            var json = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(padded));

            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("exp", out var expElement)
                && expElement.TryGetInt64(out var exp))
            {
                return DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= exp;
            }
        }
        catch
        {
            return true;
        }

        return false;
    }

    private void ApplySession(AuthSession session)
    {
        Token = session.Token;
        RefreshToken = session.RefreshToken;
        UserId = session.UserId;
        FirstName = session.FirstName;
        Email = session.Email;
        Role = session.Role;
        SessionId = session.SessionId == Guid.Empty ? Guid.NewGuid() : session.SessionId;
        RememberMe = session.RememberMe;
        IsAuthenticated = !string.IsNullOrWhiteSpace(session.Token) && !IsTokenExpired(session.Token);
        OnChange?.Invoke();
    }

    private void ClearSession()
    {
        Token = string.Empty;
        RefreshToken = string.Empty;
        UserId = string.Empty;
        FirstName = string.Empty;
        Email = string.Empty;
        Role = string.Empty;
        SessionId = Guid.Empty;
        IsAuthenticated = false;
        OnChange?.Invoke();
    }

    private class AuthSession
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid SessionId { get; set; }
        public bool RememberMe { get; set; } = true;
    }

    private class RefreshResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public int Role { get; set; }
    }
}

