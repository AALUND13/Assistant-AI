using OpenAI.Chat;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AssistantAI.DataTypes;

public enum AIResponsePermission {
    None,
    Ignored,
    Blacklisted,
}

// Tool Call Data
public class ChatToolCallData {
    [Key]
    public int Id { get; set; } // Primary key for SQLite

    [Required]
    public string ToolID { get; set; }

    [Required]
    public string FunctionName { get; set; }

    [Required]
    public string FunctionArguments { get; set; }
}

// Chat Message Data
public class ChannelChatMessageData {
    [Key]
    public int Id { get; set; } // Primary key for SQLite

    [Required]
    public ChatMessageRole Role { get; set; }

    [Required]
    public string Text { get; set; }

    public List<ChatToolCallData>? ToolCalls { get; set; } // Nullable if there are no tools

    public string? ToolCallId { get; set; } // Nullable if no ToolCallId exists
}

public class ChannelData {
    [Key]
    public long ChannelId { get; set; } // Primary key for SQLite

    public List<ChannelChatMessageData> ChatMessages { get; set; } = [];
}

// User Data
public class MemoryItem {
    [Key]
    public int Id { get; set; } // Primary key for SQLite

    [Required]
    public string Key { get; set; }

    [Required]
    public string Value { get; set; }

    // Change the foreign key to long to match the primary key in UserData
    [ForeignKey("UserData")]
    public long UserDataId { get; set; } // Foreign key for the UserData relationship

    public UserData UserData { get; set; } // Navigation property
}

public class UserData {
    [Key]
    public long UserId { get; set; } // Primary key for SQLite

    public List<ChannelChatMessageData> ChatMessages { get; set; } = [];

    public List<MemoryItem> UserMemory { get; set; } = [];

    public AIResponsePermission ResponsePermission { get; set; } = AIResponsePermission.None;
}

// Guild User Data
public class GuildUserData {
    [Key]
    public long GuildUserId { get; set; } // Primary key for SQLite

    public AIResponsePermission ResponsePermission { get; set; } = AIResponsePermission.None;

    [ForeignKey("GuildData")]
    public long GuildDataId { get; set; } // Foreign key for the GuildData relationship

    public GuildData GuildData { get; set; } // Navigation property
}


// Guild Options
public class GuildOptions {
    [Key]
    public long GuildOptionsId { get; set; } // Primary key for SQLite

    public bool Enabled { get; set; } = true;

    public string Prefix { get; set; } = "a!";
}

// Guild Data
public class GuildData {
    [Key]
    public long GuildId { get; set; } // Primary key for SQLite

    public List<GuildUserData> GuildUsers { get; set; } = [];

    public List<MemoryItem> GuildMemory { get; set; } = [];

    public GuildOptions Options { get; set; } = new();
}
