using System.Text.Json;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;

namespace SimpleDiscordNet.Rest;

internal sealed class RestClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _token;
    private readonly JsonSerializerOptions _json;
    private readonly NativeLogger _logger;
    private readonly RouteRateLimiter _rateLimiter;

    private const string BaseUrl = "https://discord.com/api/v10";

    public RestClient(HttpClient http, string token, JsonSerializerOptions json, NativeLogger logger, RouteRateLimiter limiter)
    {
        _http = http;
        _token = token;
        _json = json;
        _logger = logger;
        _rateLimiter = limiter;

        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bot", token);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SimpleDiscordNet (https://example, 1.0)");
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string route, object? payload, CancellationToken ct)
    {
        using IDisposable _ = await _rateLimiter.EnterAsync(route, ct).ConfigureAwait(false);
        using HttpRequestMessage req = new HttpRequestMessage(method, BaseUrl + route);
        if (payload != null)
        {
            req.Content = new StringContent(JsonSerializer.Serialize(payload, _json), System.Text.Encoding.UTF8, "application/json");
        }

        HttpResponseMessage res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

        if ((int)res.StatusCode == 429)
        {
            // Simple global retry handling
            int retryAfter = 1000;
            if (res.Headers.TryGetValues("Retry-After", out IEnumerable<string>? values) && int.TryParse(values.FirstOrDefault(), out int ms))
                retryAfter = ms;
            _logger.Log(LogLevel.Warning, $"Rate limited on {route}. Retrying after {retryAfter}ms");
            await Task.Delay(retryAfter, ct).ConfigureAwait(false);
            res.Dispose();
            return await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }

        return res;
    }

    public async Task<T?> GetAsync<T>(string route, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Get, route, null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on GET {route}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
        await using System.IO.Stream s = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(s, _json, ct).ConfigureAwait(false);
    }

    public async Task PutAsync(string route, object payload, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Put, route, payload, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on PUT {route}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    public async Task PostAsync(string route, object payload, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Post, route, payload, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on POST {route}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    public async Task PatchAsync(string route, object payload, CancellationToken ct)
    {
        // Use explicit PATCH method (HttpMethod.Patch is available, but construct to be safe)
        HttpMethod patch = new HttpMethod("PATCH");
        using HttpResponseMessage res = await SendAsync(patch, route, payload, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on PATCH {route}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    public async Task PostMultipartAsync(string route, object payload, (string fileName, ReadOnlyMemory<byte> data) file, CancellationToken ct)
    {
        using MultipartFormDataContent content = new MultipartFormDataContent();
        string json = JsonSerializer.Serialize(payload, _json);
        content.Add(new StringContent(json, System.Text.Encoding.UTF8, "application/json"), "payload_json");
        ByteArrayContent bytesContent = new ByteArrayContent(file.data.ToArray());
        bytesContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(bytesContent, "files[0]", file.fileName);

        using HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + route) { Content = content };
        HttpResponseMessage res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on POST (multipart) {route}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
        res.Dispose();
    }

    // ---- Convenience helpers for interactions & commands ----

    public Task<ApplicationInfo?> GetApplicationAsync(CancellationToken ct)
        => GetAsync<ApplicationInfo>("/oauth2/applications/@me", ct);

    public Task PutGuildCommandsAsync(string applicationId, string guildId, object[] commands, CancellationToken ct)
        => PutAsync($"/applications/{applicationId}/guilds/{guildId}/commands", commands, ct);

    public Task PostInteractionCallbackAsync(string interactionId, string token, object response, CancellationToken ct)
        => PostAsync($"/interactions/{interactionId}/{token}/callback", response, ct);

    public Task PostWebhookFollowupAsync(string applicationId, string token, object payload, CancellationToken ct)
        => PostAsync($"/webhooks/{applicationId}/{token}", payload, ct);

    public Task PatchWebhookOriginalAsync(string applicationId, string token, object payload, CancellationToken ct)
        => PatchAsync($"/webhooks/{applicationId}/{token}/messages/@original", payload, ct);

    public void Dispose() => _http.Dispose();
}
