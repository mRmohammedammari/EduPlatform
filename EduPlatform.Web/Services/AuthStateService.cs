using Microsoft.JSInterop;
using System.Text.Json;

namespace EduPlatform.Web.Services;

public class AuthStateService
{
    private const string StorageKey = "eduplatform-auth";
    private readonly IJSRuntime _js;

    public bool IsAuthenticated { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public Guid SessionId { get; private set; }

    public event Action? OnChange;

    private readonly TaskCompletionSource _initializationTcs = new();

    public AuthStateService(IJSRuntime js)
    {
        _js = js;
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
                var json = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var session = JsonSerializer.Deserialize<AuthSession>(json);
                    if (session is not null)
                    {
                        session.SessionId = await GetOrCreateSessionIdAsync(session.SessionId);
                        if (IsTokenExpired(session.Token))
                        {
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

    public async Task LoginAsync(string token, string userId, string firstName, string email, string role)
    {
        if (IsTokenExpired(token))
        {
            await LogoutAsync();
            return;
        }

        var session = new AuthSession
        {
            Token = token,
            UserId = userId,
            FirstName = firstName,
            Email = email,
            Role = role,
            SessionId = await GetOrCreateSessionIdAsync(Guid.Empty)
        };

        await SaveSessionAsync(session);
        ApplySession(session);
    }

    public void Login(string token, string userId, string firstName, string email, string role)
    {
        var session = new AuthSession
        {
            Token = token,
            UserId = userId,
            FirstName = firstName,
            Email = email,
            Role = role
        };

        ApplySession(session);
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            await _js.InvokeVoidAsync("eduPlatformSession.clear");
        }
        catch
        {
            // Ignore if browser storage is unavailable.
        }

        ClearSession();
    }

    public void Logout()
    {
        ClearSession();
    }

    private async Task SaveSessionAsync(AuthSession session)
    {
        try
        {
            var json = JsonSerializer.Serialize(session);
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
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
        UserId = session.UserId;
        FirstName = session.FirstName;
        Email = session.Email;
        Role = session.Role;
        SessionId = session.SessionId == Guid.Empty ? Guid.NewGuid() : session.SessionId;
        IsAuthenticated = !string.IsNullOrWhiteSpace(session.Token) && !IsTokenExpired(session.Token);
        OnChange?.Invoke();
    }

    private void ClearSession()
    {
        Token = string.Empty;
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
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public Guid SessionId { get; set; }
    }
}
