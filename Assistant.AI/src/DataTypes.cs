using OpenAI.Chat;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssistantAI.DataTypes;

public enum AIResponsePermission {
    None,
    Ignored,
    Blacklisted,
}

public class ChatToolCallData {
    [Key] public int Id { get; set; }

    public required string ToolID { get; set; }
    public required string FunctionName { get; set; }
    public required string FunctionArguments { get; set; }

    [ForeignKey("ChannelChatMessageData")] public int ChannelChatMessageDataId { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public ChannelChatMessageData ChannelChatMessageData { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}

public class ChannelChatMessageData {
    [Key] public int Id { get; set; }

    public required ChatMessageRole Role { get; set; }

    public string? Text { get; set; }
    public List<ChatToolCallData>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }

    [ForeignKey("ChannelData")] public long ChannelId { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public ChannelData Channel { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}

public class ChannelData {
    [Key] public long ChannelId { get; set; } // Primary key for SQLite

    public List<ChannelChatMessageData> ChatMessages { get; set; } = [];
}


public class UserMemoryItem {
    [Key] public int Id { get; set; }

    public required string Key { get; set; }

    public required string Value { get; set; }

    [ForeignKey("UserData")] public long UserId { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public UserData RelatedData { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable
}

public class GuildMemoryItem {
    [Key] public int Id { get; set; }

    public required string Key { get; set; }

    public required string Value { get; set; }
    [ForeignKey("GuildData")] public long GuildId { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public GuildData RelatedData { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable
}


public class UserData {
    [Key] public long UserId { get; set; }

    public List<UserMemoryItem> UserMemory { get; set; } = [];

    public AIResponsePermission ResponsePermission { get; set; } = AIResponsePermission.None;
}

public class GuildUserData {
    [Key] public long GuildUserId { get; set; }

    public AIResponsePermission ResponsePermission { get; set; } = AIResponsePermission.None;

    [ForeignKey("GuildData")] public long GuildDataId { get; set; }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public GuildData GuildData { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}


public class GuildOptions {
    [Key] public long GuildOptionsId { get; set; }

    public bool Enabled { get; set; } = true;

    public string Prefix { get; set; } = "a!";
}

public class GuildData {
    [Key] public long GuildId { get; set; }

    public List<GuildUserData> GuildUsers { get; set; } = [];

    public List<GuildMemoryItem> GuildMemory { get; set; } = [];

    public GuildOptions Options { get; set; } = new();
}
