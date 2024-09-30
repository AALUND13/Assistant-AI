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
}
