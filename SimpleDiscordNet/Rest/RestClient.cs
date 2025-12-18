using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using SimpleDiscordNet.Logging;
using SimpleDiscordNet.Models;

namespace SimpleDiscordNet.Rest;

[UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "RestClient uses JsonSerializerOptions configured with source-generated DiscordJsonContext for all known types.")]
[UnconditionalSuppressMessage("AOT", "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.", Justification = "RestClient uses JsonSerializerOptions configured with source-generated DiscordJsonContext for all known types.")]
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

    // ---- Permission management ----

    /// <summary>
    /// Edit channel permission overwrites for a user or role.
    /// </summary>
    public Task PutChannelPermissionAsync(string channelId, string overwriteId, object payload, CancellationToken ct)
        => PutAsync($"/channels/{channelId}/permissions/{overwriteId}", payload, ct);

    /// <summary>
    /// Delete a channel permission overwrite for a user or role.
    /// </summary>
    public async Task DeleteChannelPermissionAsync(string channelId, string overwriteId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/channels/{channelId}/permissions/{overwriteId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /channels/{channelId}/permissions/{overwriteId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Create a new role in a guild.
    /// </summary>
    public async Task<T?> PostGuildRoleAsync<T>(string guildId, object payload, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Post, $"/guilds/{guildId}/roles", payload, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on POST /guilds/{guildId}/roles. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
        await using System.IO.Stream s = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(s, _json, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Modify a role in a guild.
    /// </summary>
    public async Task<T?> PatchGuildRoleAsync<T>(string guildId, string roleId, object payload, CancellationToken ct)
    {
        HttpMethod patch = new HttpMethod("PATCH");
        using HttpResponseMessage res = await SendAsync(patch, $"/guilds/{guildId}/roles/{roleId}", payload, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on PATCH /guilds/{guildId}/roles/{roleId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
        await using System.IO.Stream s = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(s, _json, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete a role from a guild.
    /// </summary>
    public async Task DeleteGuildRoleAsync(string guildId, string roleId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/guilds/{guildId}/roles/{roleId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /guilds/{guildId}/roles/{roleId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    // ---- Channel management ----

    /// <summary>
    /// Create a new channel in a guild.
    /// </summary>
    public async Task<T?> PostGuildChannelAsync<T>(string guildId, object payload, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Post, $"/guilds/{guildId}/channels", payload, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on POST /guilds/{guildId}/channels. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
        await using System.IO.Stream s = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(s, _json, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Modify a channel.
    /// </summary>
    public async Task<T?> PatchChannelAsync<T>(string channelId, object payload, CancellationToken ct)
    {
        HttpMethod patch = new HttpMethod("PATCH");
        using HttpResponseMessage res = await SendAsync(patch, $"/channels/{channelId}", payload, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on PATCH /channels/{channelId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
        await using System.IO.Stream s = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(s, _json, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete a channel.
    /// </summary>
    public async Task DeleteChannelAsync(string channelId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/channels/{channelId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /channels/{channelId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    // ---- Message management ----

    /// <summary>
    /// Get a specific message from a channel.
    /// </summary>
    public Task<T?> GetChannelMessageAsync<T>(string channelId, string messageId, CancellationToken ct)
        => GetAsync<T>($"/channels/{channelId}/messages/{messageId}", ct);

    /// <summary>
    /// Get multiple messages from a channel.
    /// </summary>
    public Task<T?> GetChannelMessagesAsync<T>(string channelId, int limit, string? before, string? after, CancellationToken ct)
    {
        string query = $"?limit={limit}";
        if (before != null) query += $"&before={before}";
        if (after != null) query += $"&after={after}";
        return GetAsync<T>($"/channels/{channelId}/messages{query}", ct);
    }

    /// <summary>
    /// Edit a message.
    /// </summary>
    public async Task<T?> PatchMessageAsync<T>(string channelId, string messageId, object payload, CancellationToken ct)
    {
        HttpMethod patch = new HttpMethod("PATCH");
        using HttpResponseMessage res = await SendAsync(patch, $"/channels/{channelId}/messages/{messageId}", payload, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on PATCH /channels/{channelId}/messages/{messageId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
        await using System.IO.Stream s = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(s, _json, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete a message.
    /// </summary>
    public async Task DeleteMessageAsync(string channelId, string messageId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/channels/{channelId}/messages/{messageId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /channels/{channelId}/messages/{messageId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Bulk delete messages (2-100 messages, not older than 14 days).
    /// </summary>
    public async Task BulkDeleteMessagesAsync(string channelId, string[] messageIds, CancellationToken ct)
    {
        var payload = new { messages = messageIds };
        using HttpResponseMessage res = await SendAsync(HttpMethod.Post, $"/channels/{channelId}/messages/bulk-delete", payload, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on POST /channels/{channelId}/messages/bulk-delete. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    // ---- Reaction management ----

    /// <summary>
    /// Add a reaction to a message.
    /// </summary>
    public async Task AddReactionAsync(string channelId, string messageId, string emoji, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Put, $"/channels/{channelId}/messages/{messageId}/reactions/{emoji}/@me", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on PUT /channels/{channelId}/messages/{messageId}/reactions/{emoji}/@me. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Remove own reaction from a message.
    /// </summary>
    public async Task RemoveOwnReactionAsync(string channelId, string messageId, string emoji, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/channels/{channelId}/messages/{messageId}/reactions/{emoji}/@me", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /channels/{channelId}/messages/{messageId}/reactions/{emoji}/@me. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Remove a user's reaction from a message.
    /// </summary>
    public async Task RemoveUserReactionAsync(string channelId, string messageId, string emoji, string userId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/channels/{channelId}/messages/{messageId}/reactions/{emoji}/{userId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /channels/{channelId}/messages/{messageId}/reactions/{emoji}/{userId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Get users who reacted with a specific emoji.
    /// </summary>
    public Task<T?> GetReactionsAsync<T>(string channelId, string messageId, string emoji, int limit, CancellationToken ct)
        => GetAsync<T>($"/channels/{channelId}/messages/{messageId}/reactions/{emoji}?limit={limit}", ct);

    /// <summary>
    /// Remove all reactions from a message.
    /// </summary>
    public async Task RemoveAllReactionsAsync(string channelId, string messageId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/channels/{channelId}/messages/{messageId}/reactions", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /channels/{channelId}/messages/{messageId}/reactions. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Remove all reactions for a specific emoji from a message.
    /// </summary>
    public async Task RemoveAllReactionsForEmojiAsync(string channelId, string messageId, string emoji, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/channels/{channelId}/messages/{messageId}/reactions/{emoji}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /channels/{channelId}/messages/{messageId}/reactions/{emoji}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    // ---- Pin management ----

    /// <summary>
    /// Pin a message in a channel.
    /// </summary>
    public async Task PinMessageAsync(string channelId, string messageId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Put, $"/channels/{channelId}/pins/{messageId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on PUT /channels/{channelId}/pins/{messageId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Unpin a message in a channel.
    /// </summary>
    public async Task UnpinMessageAsync(string channelId, string messageId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/channels/{channelId}/pins/{messageId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /channels/{channelId}/pins/{messageId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Get all pinned messages in a channel.
    /// </summary>
    public Task<T?> GetPinnedMessagesAsync<T>(string channelId, CancellationToken ct)
        => GetAsync<T>($"/channels/{channelId}/pins", ct);

    // ---- Member moderation ----

    /// <summary>
    /// Kick a member from a guild (remove guild member).
    /// </summary>
    public async Task KickMemberAsync(string guildId, string userId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/guilds/{guildId}/members/{userId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /guilds/{guildId}/members/{userId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Ban a member from a guild.
    /// </summary>
    public async Task BanMemberAsync(string guildId, string userId, int? deleteMessageDays, CancellationToken ct)
    {
        var payload = new { delete_message_days = deleteMessageDays };
        using HttpResponseMessage res = await SendAsync(HttpMethod.Put, $"/guilds/{guildId}/bans/{userId}", payload, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on PUT /guilds/{guildId}/bans/{userId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Unban a user from a guild.
    /// </summary>
    public async Task UnbanMemberAsync(string guildId, string userId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/guilds/{guildId}/bans/{userId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /guilds/{guildId}/bans/{userId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    // ---- Role assignment ----

    /// <summary>
    /// Add a role to a guild member.
    /// </summary>
    public async Task AddMemberRoleAsync(string guildId, string userId, string roleId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Put, $"/guilds/{guildId}/members/{userId}/roles/{roleId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on PUT /guilds/{guildId}/members/{userId}/roles/{roleId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Remove a role from a guild member.
    /// </summary>
    public async Task RemoveMemberRoleAsync(string guildId, string userId, string roleId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/guilds/{guildId}/members/{userId}/roles/{roleId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /guilds/{guildId}/members/{userId}/roles/{roleId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    // ---- Typing indicator ----

    /// <summary>
    /// Trigger typing indicator in a channel (lasts 10 seconds or until a message is sent).
    /// </summary>
    public async Task TriggerTypingIndicatorAsync(string channelId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Post, $"/channels/{channelId}/typing", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on POST /channels/{channelId}/typing. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    // ---- Thread operations ----

    /// <summary>
    /// Join a thread.
    /// </summary>
    public async Task JoinThreadAsync(string threadId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Put, $"/channels/{threadId}/thread-members/@me", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on PUT /channels/{threadId}/thread-members/@me. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Leave a thread.
    /// </summary>
    public async Task LeaveThreadAsync(string threadId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/channels/{threadId}/thread-members/@me", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /channels/{threadId}/thread-members/@me. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Add a member to a thread.
    /// </summary>
    public async Task AddThreadMemberAsync(string threadId, string userId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Put, $"/channels/{threadId}/thread-members/{userId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on PUT /channels/{threadId}/thread-members/{userId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Remove a member from a thread.
    /// </summary>
    public async Task RemoveThreadMemberAsync(string threadId, string userId, CancellationToken ct)
    {
        using HttpResponseMessage res = await SendAsync(HttpMethod.Delete, $"/channels/{threadId}/thread-members/{userId}", null, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode)
        {
            string? body = null;
            try { body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { }
            _logger.Log(LogLevel.Error, $"HTTP {((int)res.StatusCode)} on DELETE /channels/{threadId}/thread-members/{userId}. Body: {body}");
        }
        res.EnsureSuccessStatusCode();
    }

    public void Dispose() => _http.Dispose();
}
