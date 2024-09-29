using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using System.ComponentModel;

namespace AssistantAI.Events {
    public partial class AssistantAIGuild {
        [Description("Get information about a user.")]
        string GetUserInfo([Description("The user ID to get information about.")] ulong userID) {
            DiscordUser? user = client.GetUserAsync(userID).Result
                ?? throw new ArgumentException($"User with ID {userID} was not found.");

            return $"[User: {user.GlobalName ?? user.Username} | ID: {user.Id}]";
        }
    }
}
