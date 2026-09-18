using System.Net.Http.Headers;

namespace EduPlatform.Web.Services;

/// <summary>
/// Centralizes attaching the current user's JWT to outgoing API requests,
/// avoiding per-page duplication of the Authorization header logic.
/// </summary>
public static class HttpClientFactoryExtensions
{
    public static HttpClient CreateAuthorizedClient(this IHttpClientFactory factory, AuthStateService authState)
    {
        var client = factory.CreateClient("EduPlatformAPI");
        if (!string.IsNullOrEmpty(authState.Token))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authState.Token);
        }
        return client;
    }
}
