using System.Text.Json.Serialization;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Context;
using SimpleDiscordNet.Models.Requests;

namespace SimpleDiscordNet.Serialization;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata,
                             PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
                             WriteIndented = false)]
[JsonSerializable(typeof(ApplicationInfo))]
[JsonSerializable(typeof(Guild))]
[JsonSerializable(typeof(Guild[]))]
[JsonSerializable(typeof(Channel))]
[JsonSerializable(typeof(Role))]
[JsonSerializable(typeof(Member))]
[JsonSerializable(typeof(Member[]))]
[JsonSerializable(typeof(Embed))]
[JsonSerializable(typeof(CreateMessageRequest))]
[JsonSerializable(typeof(WebhookMessageRequest))]
[JsonSerializable(typeof(OpenModalRequest))]
[JsonSerializable(typeof(ModalData))]
[JsonSerializable(typeof(ApplicationCommandDefinition))]
[JsonSerializable(typeof(ApplicationCommandDefinition[]))]
[JsonSerializable(typeof(ChannelWithGuild))]
[JsonSerializable(typeof(MemberWithGuild))]
[JsonSerializable(typeof(RoleWithGuild))]
[JsonSerializable(typeof(UserWithGuild))]
[JsonSerializable(typeof(GatewayPayload))]
[JsonSerializable(typeof(Identify))]
[JsonSerializable(typeof(IdentifyPayload))]
[JsonSerializable(typeof(IdentifyConnectionProperties))]
[JsonSerializable(typeof(Heartbeat))]
[JsonSerializable(typeof(Resume))]
[JsonSerializable(typeof(ResumePayload))]

internal partial class DiscordJsonContext : JsonSerializerContext;
