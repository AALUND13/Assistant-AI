using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using System.ComponentModel.DataAnnotations;

namespace AssistantAI.DataTypes;

public enum AIResponsePermission {
    None,
    Ignored,
    Blacklisted,
}



public class ChatMessageContentPartData {
    public int Id { get; set; } // Primary key for SQLite

    public string Text { get; set; }

    public ChatMessageContentPartData(string text) {
        Text = text;
    }
}

public class ChatToolCallData {
    public int Id { get; set; } // Primary key for SQLite

    public string ToolID { get; set; }
    public string FunctionName { get; set; }
    public string FunctionArguments { get; set; }

    public ChatToolCallData(string toolID, string functionName, string functionArguments) {
        ToolID = toolID;
        FunctionName = functionName;
        FunctionArguments = functionArguments;
    }
}

public class ChatMessageData {
    public int Id { get; set; } // Primary key for SQLite

    public ChatMessageRole Role { get; set; }
    public List<ChatMessageContentPartData> ContentParts { get; set; }
    public List<ChatToolCallData>? ToolCalls { get; set; }
    public string? ToolCallId { get; set; }

    public ChatMessageData(ChatMessageRole role, List<ChatMessageContentPartData> contentParts, List<ChatToolCallData>? toolCalls, string? toolCallId) {
        Role = role;
        ContentParts = contentParts;
        ToolCalls = toolCalls;
        ToolCallId = toolCallId;
    }
}

// User data
public class MemoryItem {
    public int Id { get; set; } // Primary key for SQLite

    public string Key { get; set; }
    public string Value { get; set; }

    public int UserDataId { get; set; } // Foreign key for the UserData relationship
    public UserData UserData { get; set; } // Navigation property

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public MemoryItem(string key, string value) {
        Key = key;
        Value = value;
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}

public class UserData {
    public long Id { get; set; } // Primary key for SQLite

    public List<ChatMessageData> ChatMessages { get; set; } = [];
    public List<MemoryItem> Memory { get; set; } = [];

    public AIResponsePermission ResponsePermission { get; set; } = AIResponsePermission.None;
}

// Guild data

public class GuildUserData {
    public long Id { get; set; } // Primary key for SQLite

    public AIResponsePermission ResponsePermission { get; set; } = AIResponsePermission.None;
    public GuildOptions Options { get; set; } = new();

    public int GuildDataId { get; set; } // Foreign key for the GuildData relationship
    public GuildData GuildData { get; set; } // Navigation property
}

public class GuildOptions {
    public bool Enabled = true;
    public string Prefix = "a!";
}

public class GuildData {
    public long GuildId { get; set; } // Primary key for SQLite

    public List<GuildUserData> GuildUsers { get; set; } = [];
    public List<MemoryItem> GuildMemory { get; set; } = [];
    public GuildOptions Options { get; set; } = new();
}