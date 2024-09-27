using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using System.ComponentModel;

namespace AssistantAI.Events {
    public partial class AssistantAIGuild {
        [Description("Join the voice channel of a user.")]
        void JoinUserVC([Description("The user ID to join the voice channel of.")] ulong userID) {
            DiscordChannel? channel = (client.Guilds.Values.SelectMany(guild => guild.VoiceStates.Values)
                .FirstOrDefault(voiceState => voiceState.User.Id == userID)?.Channel)
                ?? throw new ArgumentException($"User with ID {userID} is not in a voice channel.");

            channel.ConnectAsync().Wait();
        }

        [Description("Get information about a user.")]
        string GetUserInfo([Description("The user ID to get information about.")] ulong userID) {
            DiscordUser? user = client.GetUserAsync(userID).Result
                ?? throw new ArgumentException($"User with ID {userID} was not found.");

            return $"[User: {user.GlobalName ?? user.Username} | ID: {user.Id}]";
        }
    }
}
