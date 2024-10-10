using AssistantAI.AiModule.Attributes;
using OpenAI.Chat;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssistantAI;

public enum AIResponsePermission {
    None,
    Ignored,
    Blacklisted,
}

#nullable disable

public class ChannelMention {
    [Key] public int Id { get; set; }
    public ulong ChannelId { get; set; }

    override public string ToString() {
        return $"<#{ChannelId}>";
    }

    [ForeignKey("GuildOptions")] public ulong GuildOptionsId { get; set; }
    public GuildOptions GuildOptions { get; set; }
}

public class ChatToolCallData {
    [Key] public int Id { get; set; }

    public required string ToolID { get; set; }
    public required string FunctionName { get; set; }
    public required string FunctionArguments { get; set; }

    [ForeignKey("ChannelChatMessageData")] public int ChannelChatMessageDataId { get; set; }
    public ChannelChatMessageData ChannelChatMessageData { get; set; }

}

public class ChannelChatMessageData {
    [Key] public int Id { get; set; }

    public required ChatMessageRole Role { get; set; }

    public string? Text { get; set; }
    public List<ChatToolCallData>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }

    [ForeignKey("ChannelData")] public ulong ChannelId { get; set; }
    public ChannelData Channel { get; set; }

}

public class ChannelData {
    [Key] public ulong ChannelId { get; set; } // Primary key for SQLite

    public List<ChannelChatMessageData> ChatMessages { get; set; } = [];
}


public class UserMemoryItem {
    [Key] public int Id { get; set; }

    public required string Key { get; set; }

    public required string Value { get; set; }

    [ForeignKey("UserData")] public ulong UserId { get; set; }
    public UserData RelatedData { get; set; }
}

public class GuildMemoryItem {
    [Key] public int Id { get; set; }

    public required string Key { get; set; }

    public required string Value { get; set; }

    [ForeignKey("GuildData")] public ulong GuildId { get; set; }
    public GuildData RelatedData { get; set; }
}


public class UserData {
    [Key] public ulong UserId { get; set; }

    public List<UserMemoryItem> UserMemory { get; set; } = [];

    public AIResponsePermission ResponsePermission { get; set; } = AIResponsePermission.None;
}

public class GuildUserData {
    [Key] public ulong GuildUserId { get; set; }

    public AIResponsePermission ResponsePermission { get; set; } = AIResponsePermission.None;

    [ForeignKey("GuildData")] public ulong GuildDataId { get; set; }
    public GuildData GuildData { get; set; }

}


public class GuildOptions {
    [Key, Ignore] public ulong GuildOptionsId { get; set; }

    // Options.

    public bool AIEnabled { get; set; } = true;
    public string Prefix { get; set; } = "a!";
    [Ignore] public List<ChannelMention> ChannelWhitelists { get; set; } = [];
    [NotMapped] public List<ulong> ChannelWhitelistIds { get { 
            return ChannelWhitelists.Select(c => c.ChannelId).ToList(); 
        } set { 
            ChannelWhitelists = value.Select(c => new ChannelMention { ChannelId = c }).ToList(); 
        } 
    }

    // End of options.

    [ForeignKey("GuildData"), Ignore] public ulong GuildDataId { get; set; }
    [Ignore] public GuildData GuildData { get; set; }

}

public class GuildData {
    [Key] public ulong GuildId { get; set; }

    public List<GuildUserData> GuildUsers { get; set; } = [];

    public List<GuildMemoryItem> GuildMemory { get; set; } = [];

    public GuildOptions Options { get; set; } = new();
}

#nullable restore