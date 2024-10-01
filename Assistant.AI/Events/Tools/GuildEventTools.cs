using DSharpPlus.Entities;
using System.ComponentModel;
using System.Text;

namespace AssistantAI.Events;

// All the tools methods of the GuildEvent class.
public partial class GuildEvent {
    [Description("Get information about a user.")]
    string GetUserInfo([Description("The user ID to get information about.")] ulong userID) {
        DiscordUser user = client.GetUserAsync(userID).Result
            ?? throw new ArgumentException($"User with ID {userID} was not found.");

        string? customActivity = user.Presence?.Activities.FirstOrDefault(activity => activity.ActivityType == DiscordActivityType.Custom)?.RichPresence.State;

        var stringBuilder = new StringBuilder();
        stringBuilder.Append($"User: {user.GlobalName ?? user.Username}");
        stringBuilder.Append($" | Status: {Enum.GetName(user.Presence?.Status ?? DiscordUserStatus.Offline)}");
        if(customActivity != null)
            stringBuilder.Append($" | Custom Activity: {customActivity}");

        return $"[{stringBuilder}]";
    }

    [Description("Add or overwrite a key in the user memory.")]
    string AddOrOverwriteUserMemory([Description("The user ID to get information about.")] ulong userID, [Description("The key to add or overwrite.")] string key, [Description("The value to add or overwrite.")] string value) {
        if(!UserMemory.ContainsKey(userID))
            UserMemory[userID] = [];

        UserMemory[userID][key] = value;
        return $"User memory key {key} has been added or overwritten with value {value}";
    }

    [Description("Add or overwrite a key in the guild memory.")]
    string AddOrOverwriteGuildMemory([Description("The user ID to get information about.")] ulong guildID, [Description("The key to add or overwrite.")] string key, [Description("The value to add or overwrite.")] string value) {
        if(!GuildMemory.ContainsKey(guildID))
            GuildMemory[guildID] = [];

        GuildMemory[guildID][key] = value;
        return $"Guild memory key {key} has been added or overwritten with value {value}";
    }

    [Description("Remove a key from the user memory.")]
    string RemoveUserMemory([Description("The user ID to get information about.")] ulong userID, [Description("The key to remove.")] string key) {
        if(!UserMemory.ContainsKey(userID))
            UserMemory[userID] = [];

        if(UserMemory[userID].Remove(key))
            return $"User memory key {key} has been removed";
        return $"User memory key {key} was not found";
    }

    [Description("Remove a key from the guild memory.")]
    string RemoveGuildMemory([Description("The user ID to get information about.")] ulong guildID, [Description("The key to remove.")] string key) {
        if(!GuildMemory.ContainsKey(guildID))
            GuildMemory[guildID] = [];

        if(GuildMemory[guildID].Remove(key))
            return $"Guild memory key {key} has been removed";
        return $"Guild memory key {key} was not found";
    }
}
