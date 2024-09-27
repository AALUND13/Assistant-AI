using OpenAI.Chat;

namespace AssistantAI.DataTypes;

public enum BlacklistStatus {
    Blacklisted,
    Ingored
}

public record struct ChatMessageContentPartData(string Text, Uri ImageUri);
public record struct ChatToolCallData(string Id, string FunctionName, string FunctionArguments);

public record struct ChatMessageData(ChatMessageRole Role, List<ChatMessageContentPartData> ContentParts, List<ChatToolCallData>? ToolCalls, string? ToolCallId);

public struct UserData() {
    public Dictionary<string, long> CommandCooldowns = [];
}

public struct ChannelData() {
    public List<ChatMessageData> ChatMessages = [];
}


public struct GuildData() {
    public List<(ulong userID, BlacklistStatus blacklistStatus)> BlacklistedUsers = [];
}

public struct Data() {
    public Dictionary<ulong, UserData> Users = [];
    public Dictionary<ulong, GuildData> GuildData = [];
    public Dictionary<ulong, ChannelData> ChannelData = [];

    public readonly UserData GetOrDefaultUserData(ulong userID) {
        Users.TryAdd(userID, new UserData());
        return Users[userID];
    }

    public readonly ChannelData GetOrDefaultChannelData(ulong channelID) {
        ChannelData.TryAdd(channelID, new ChannelData());
        return ChannelData[channelID];
    }

    public readonly GuildData GetOrDefaultGuildData(ulong guildID) {
        GuildData.TryAdd(guildID, new GuildData());
        return GuildData[guildID];
    }
}
