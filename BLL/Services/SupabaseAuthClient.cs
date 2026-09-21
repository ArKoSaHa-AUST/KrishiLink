using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace KrishiLink.BLL.Services;

public sealed class SupabaseAuthOptions
{
    public const string SectionName = "Supabase";
    public string Url { get; set; } = "";
    public string PublishableKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string AdminEmail { get; set; } = "";
}

public sealed class SupabaseAuthException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

public sealed class SupabaseAuthUser
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("email_confirmed_at")] public DateTimeOffset? EmailConfirmedAt { get; set; }
}

public sealed class SupabaseAuthTokens
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = "";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("user")] public SupabaseAuthUser User { get; set; } = new();
}

/// <summary>Only this server calls Auth; neither service keys nor session tokens go to browser code.</summary>
public sealed class SupabaseAuthClient(HttpClient http, IOptions<SupabaseAuthOptions> options)
{
    private readonly SupabaseAuthOptions _options = options.Value;

    public Task<SupabaseAuthUser> CreateUserAsync(string email, string password, bool confirmed = false) =>
        SendAsync<SupabaseAuthUser>(HttpMethod.Post, "admin/users",
            new { email, password, email_confirm = confirmed }, admin: true);

    public async Task DeleteUserAsync(string id) =>
        _ = await SendAsync<JsonElement>(HttpMethod.Delete, $"admin/users/{Uri.EscapeDataString(id)}", admin: true);

    public Task<SupabaseAuthTokens> SignInAsync(string email, string password) =>
        SendAsync<SupabaseAuthTokens>(HttpMethod.Post, "token?grant_type=password", new { email, password });

    public Task<SupabaseAuthTokens> RefreshAsync(string refreshToken) =>
        SendAsync<SupabaseAuthTokens>(HttpMethod.Post, "token?grant_type=refresh_token", new { refresh_token = refreshToken });

    public Task<SupabaseAuthUser> GetUserAsync(string accessToken) =>
        SendAsync<SupabaseAuthUser>(HttpMethod.Get, "user", accessToken: accessToken);

    public Task<SupabaseAuthTokens> VerifyAsync(string email, string token, string type) =>
        SendAsync<SupabaseAuthTokens>(HttpMethod.Post, "verify", new { email, token, type });

    public Task<SupabaseAuthTokens> VerifyHashAsync(string tokenHash, string type) =>
        SendAsync<SupabaseAuthTokens>(HttpMethod.Post, "verify", new { token_hash = tokenHash, type });

    public async Task SendConfirmationAsync(string email) =>
        _ = await SendAsync<JsonElement>(HttpMethod.Post, "resend", new { email, type = "signup" });

    public async Task SendRecoveryAsync(string email) =>
        _ = await SendAsync<JsonElement>(HttpMethod.Post, "recover", new { email });

    public Task<SupabaseAuthUser> ChangePasswordAsync(string accessToken, string password) =>
        SendAsync<SupabaseAuthUser>(HttpMethod.Put, "user", new { password }, accessToken);

    public Task<SupabaseAuthUser> ChangeEmailAsync(string accessToken, string email) =>
        SendAsync<SupabaseAuthUser>(HttpMethod.Put, "user", new { email }, accessToken);

    public async Task LogoutAsync(string accessToken, bool allSessions = false) =>
        _ = await SendAsync<JsonElement>(HttpMethod.Post,
            $"logout?scope={(allSessions ? "global" : "local")}", accessToken: accessToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null,
        string? accessToken = null, bool admin = false)
    {
        if (!Uri.TryCreate(_options.Url, UriKind.Absolute, out var root) ||
            (root.Scheme != Uri.UriSchemeHttps && !(root.Scheme == Uri.UriSchemeHttp && root.IsLoopback)) ||
            string.IsNullOrWhiteSpace(_options.PublishableKey) || string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new SupabaseAuthException("Authentication is not configured. Please contact the administrator.");

        using var request = new HttpRequestMessage(method, $"{root.AbsoluteUri.TrimEnd('/')}/auth/v1/{path}");
        var key = admin ? _options.SecretKey : _options.PublishableKey;
        request.Headers.Add("apikey", key);
        // Legacy service_role keys are JWTs; modern sb_secret_* keys use only the apikey header.
        if (accessToken != null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        else if (admin && !key.StartsWith("sb_secret_", StringComparison.Ordinal))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        if (body != null) request.Content = JsonContent.Create(body);

        try
        {
            using var response = await http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode == HttpStatusCode.TooManyRequests
                    ? "Too many authentication attempts. Please wait before trying again."
                    : "The authentication request could not be completed. Check your details or request a new email code.";
                // Never expose upstream response bodies (which may contain account details or tokens).
                throw new SupabaseAuthException(message, response.StatusCode);
            }
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(content)) return default!;
            return JsonSerializer.Deserialize<T>(content)
                ?? throw new SupabaseAuthException("The authentication service returned an invalid response.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new SupabaseAuthException("Authentication is temporarily unavailable. Please try again.");
        }
    }
}
