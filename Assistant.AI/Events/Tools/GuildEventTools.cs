using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using System.ComponentModel;
using System.Text;

namespace AssistantAI.Events;

// All the tools methods of the GuildEvent class.
public partial class GuildEvent {
    private string GetActivity(DiscordActivity activity) {
        return activity.ActivityType switch {
            DiscordActivityType.Custom => $"Custom Activity: {activity.RichPresence.State ?? "Unknow"}",
            DiscordActivityType.ListeningTo => $"Listening on {activity.Name} to {activity.RichPresence.Details}",
            DiscordActivityType.Playing => $"Playing: {activity.Name}",
            DiscordActivityType.Streaming => $"Streaming: {activity.Name}",
            DiscordActivityType.Watching => $"Watching: {activity.Name}",
            _ => "Unknown Activity"
        };
    }

    [Description("Get information about a user. List the user's name, status, and activities.")]
    async Task<string> GetUserInfo([Description("The user ID to get information about.")] ulong userID) {
        DiscordUser? user;
        try {
            user = await client.GetUserAsync(userID);
        } catch(BadRequestException) {
            return $"User with ID '{userID}' was not found.";
        }

        StringBuilder ActivitiesStringBuilder = new();
        foreach(var activity in user.Presence.Activities) {
            ActivitiesStringBuilder.Append($"{GetActivity(activity)}\n");
        }

        var stringBuilder = new StringBuilder();
        stringBuilder.Append($"User: {user.GlobalName ?? user.Username}\n");
        stringBuilder.Append($"Status: {Enum.GetName(user.Presence?.Status ?? DiscordUserStatus.Offline)}\n");
        if(ActivitiesStringBuilder.Length > 0)
            stringBuilder.Append(ActivitiesStringBuilder.ToString());

        return $"{stringBuilder}";
    }

    //TODO: Make these tools methods with the new chat message system.

    [Description("Add or overwrite a key in the user memory, Note: The memory not update until next message.")]
    string AddOrOverwriteUserMemory(ToolTrigger toolTrigger, [Description("The key to add or overwrite.")] string key, [Description("The value to add or overwrite.")] string value) {
        if(!UserMemory.ContainsKey(toolTrigger.User!.Id)) {
            UserMemory[toolTrigger.User.Id] = [];
            AddEventForUserMemory(toolTrigger.User.Id);
        }

        UserMemory[toolTrigger.User.Id].AddItem(new(key, value));
        return $"User memory key '{key}' has been added or overwritten with value '{value}'.";
    }

    [Description("Add or overwrite a key in the guild memory, Note: The memory not update until next message.")]
    string AddOrOverwriteGuildMemory(ToolTrigger toolTrigger, [Description("The key to add or overwrite.")] string key, [Description("The value to add or overwrite.")] string value) {
        if(!GuildMemory.ContainsKey(toolTrigger.Guild!.Id)) {
            GuildMemory[toolTrigger.Guild.Id] = [];
            AddEventForGuildMemory(toolTrigger.Guild.Id);
        }

        GuildMemory[toolTrigger.Guild.Id].AddItem(new(key, value));
        return $"Guild memory key '{key}' has been added or overwritten with value '{value}'.";
    }

    [Description("Remove a key from the user memory, Note: The memory not update until next message.")]
    string RemoveUserMemory(ToolTrigger toolTrigger, [Description("The key to remove.")] string key) {
        if(!UserMemory.ContainsKey(toolTrigger.User!.Id))
            return $"User memory key '{key}' was not found.";

        var keyValuePair = UserMemory[toolTrigger.User.Id].FirstOrDefault(keyValuePair => keyValuePair.Key == key);

        if(UserMemory[toolTrigger.User.Id].RemoveItem(keyValuePair))
            return $"User memory key '{key}' has been removed.";

        return $"User memory key '{key}' was not found.";
    }

    [Description("Remove a key from the guild memory, Note: The memory not update until next message.")]
    string RemoveGuildMemory(ToolTrigger toolTrigger, [Description("The key to remove.")] string key) {
        if(!GuildMemory.ContainsKey(toolTrigger.Guild!.Id))
            return $"Guild memory key '{key}' was not found.";

        var keyValuePair = GuildMemory[toolTrigger.Guild.Id].FirstOrDefault(keyValuePair => keyValuePair.Key == key);

        if(GuildMemory[toolTrigger.Guild.Id].RemoveItem(keyValuePair))
            return $"Guild memory key '{key}' has been removed.";
        return $"Guild memory key '{key}' was not found.";
    }

    [Description("Get the user memory.")]
    string GetUserMemory([Description("The user ID to get memory from.")] ulong userID) {
        if(!UserMemory.ContainsKey(userID))
            return "User memory is empty";

        var stringBuilder = new StringBuilder();
        stringBuilder.Append("User memory: ");
        foreach(var (key, value) in UserMemory[userID])
            stringBuilder.Append($"{key}: {value}, ");

        return stringBuilder.ToString();
    }
}
