using System.Text.Json.Serialization;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Requests;

namespace SimpleDiscordNet.Serialization;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata,
                             PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
                             WriteIndented = false)]
[JsonSerializable(typeof(ApplicationInfo))]
[JsonSerializable(typeof(Guild))]
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
internal partial class DiscordJsonContext : JsonSerializerContext;
