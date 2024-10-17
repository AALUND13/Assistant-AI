using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace AssistantAI.DiscordUtilities.EventArgs {
    public class HelpFormatterEventArgs : System.EventArgs {
        DiscordGuild? Guild { get; init; }
        DiscordChannel? Channel { get; init; }
        DiscordMessage? Message { get; init; }
        DiscordInteraction? Interaction { get; init; }
        CommandsExtension? CommandsExtension { get; init; }

        public int CategoryIndex { get; set; }
    }
}
