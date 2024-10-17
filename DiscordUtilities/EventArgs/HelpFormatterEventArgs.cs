using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace AssistantAI.DiscordUtilities.EventArgs {
    public class HelpFormatterEventArgs : System.EventArgs {
        public DiscordGuild? Guild { get; init; }
        public DiscordChannel? Channel { get; init; }
        public DiscordMessage? Message { get; init; }
        public DiscordInteraction? Interaction { get; init; }
        public CommandsExtension? CommandsExtension { get; init; }

        public int CategoryIndex { get; set; }
    }
}
