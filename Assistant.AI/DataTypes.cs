using OpenAI.Chat;

namespace AssistantAI.DataTypes;

public enum AIResponsePermission {
    None,
    Ignored,
    Blacklisted,
}

public record struct ChatMessageContentPartData(string Text, Uri? ImageUri);
public record struct ChatToolCallData(string Id, string FunctionName, string FunctionArguments);
public record struct ChatMessageData(ChatMessageRole Role, List<ChatMessageContentPartData> ContentParts, List<ChatToolCallData>? ToolCalls, string? ToolCallId);

public class UserData {
    public Dictionary<string, long> CommandCooldowns = [];
}

public class ChannelData {
    public List<ChatMessageData> ChatMessages = [];
}

public class GuildUserData {
    public AIResponsePermission ResponsePermission = AIResponsePermission.None;
}

public class GuildData {
    public Dictionary<ulong, GuildUserData> GuildUsers = [];

    public GuildUserData GetOrDefaultGuildUser(ulong userID) {
        if(!GuildUsers.ContainsKey(userID)) {
            GuildUsers[userID] = new GuildUserData();
        }
        return GuildUsers[userID];
    }
}

public class Data {
    public Dictionary<ulong, UserData> Users = [];
    public Dictionary<ulong, GuildData> Guilds = [];
    public Dictionary<ulong, ChannelData> Channels = [];

    public UserData GetOrDefaultUser(ulong userID) {
        if(!Users.ContainsKey(userID)) {
            Users[userID] = new UserData();
        }
        return Users[userID];
    }

    public ChannelData GetOrDefaultChannel(ulong channelID) {
        if(!Channels.ContainsKey(channelID)) {
            Channels[channelID] = new ChannelData();
        }
        return Channels[channelID];
    }

    public GuildData GetOrDefaultGuild(ulong guildID) {
        if(!Guilds.ContainsKey(guildID)) {
            Guilds[guildID] = new GuildData();
        }
        return Guilds[guildID];
    }
}
