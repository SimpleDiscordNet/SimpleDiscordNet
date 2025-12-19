using System.Text.Json.Serialization;
using SimpleDiscordNet.Entities;
using SimpleDiscordNet.Gateway;
using SimpleDiscordNet.Models;
using SimpleDiscordNet.Models.Context;
using SimpleDiscordNet.Models.Requests;
using SimpleDiscordNet.Primitives;

namespace SimpleDiscordNet.Serialization;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata,
                             PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
                             WriteIndented = false)]
[JsonSerializable(typeof(ApplicationInfo))]
[JsonSerializable(typeof(Guild))]
[JsonSerializable(typeof(Guild[]))]
[JsonSerializable(typeof(Channel))]
[JsonSerializable(typeof(Channel[]))]
[JsonSerializable(typeof(Role))]
[JsonSerializable(typeof(Role[]))]
[JsonSerializable(typeof(Member))]
[JsonSerializable(typeof(Member[]))]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(User[]))]
[JsonSerializable(typeof(Emoji))]
[JsonSerializable(typeof(Emoji[]))]
[JsonSerializable(typeof(Message))]
[JsonSerializable(typeof(Embed))]
[JsonSerializable(typeof(CreateMessageRequest))]
[JsonSerializable(typeof(WebhookMessageRequest))]
[JsonSerializable(typeof(OpenModalRequest))]
[JsonSerializable(typeof(ModalData))]
[JsonSerializable(typeof(ApplicationCommandDefinition))]
[JsonSerializable(typeof(ApplicationCommandDefinition[]))]
[JsonSerializable(typeof(CommandChoice))]
[JsonSerializable(typeof(InteractionResponse))]
[JsonSerializable(typeof(InteractionResponseData))]
[JsonSerializable(typeof(InteractionCreateEvent))]
[JsonSerializable(typeof(InteractionOption))]
[JsonSerializable(typeof(MessageComponentData))]
[JsonSerializable(typeof(ActionRow))]
[JsonSerializable(typeof(Button))]
[JsonSerializable(typeof(StringSelect))]
[JsonSerializable(typeof(SelectOption))]
[JsonSerializable(typeof(UserSelect))]
[JsonSerializable(typeof(RoleSelect))]
[JsonSerializable(typeof(MentionableSelect))]
[JsonSerializable(typeof(ChannelSelect))]
[JsonSerializable(typeof(TextInput))]
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
[JsonSerializable(typeof(RequestGuildMembers))]
[JsonSerializable(typeof(RequestGuildMembersPayload))]

internal partial class DiscordJsonContext : JsonSerializerContext;
