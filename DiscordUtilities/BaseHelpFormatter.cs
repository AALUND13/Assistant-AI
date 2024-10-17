using AssistantAI.DiscordUtilities.EventArgs;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;

namespace AssistantAI.DiscordUtilities {
    public abstract class BaseHelpFormatter {
        private static DiscordButtonComponent[] buttons = [
            new DiscordButtonComponent(DiscordButtonStyle.Primary, "previous-category", "Previous Category"),
            new DiscordButtonComponent(DiscordButtonStyle.Primary, "next-category", "Next Category")
        ];

        public abstract IEnumerable<DiscordMessageBuilder> FormatHelpMessage(Dictionary<string, IEnumerable<Command>> commandCategories, HelpFormatterEventArgs args);
        public abstract Dictionary<string, IEnumerable<Command>> GetCommandCategories(IEnumerable<Command> commands);

        protected DiscordMessageBuilder BuildHelpMessages(CommandsExtension commandsExtension, HelpFormatterEventArgs args) {
            Dictionary<string, IEnumerable<Command>> commandCategories = GetCommandCategories(commandsExtension.Commands.Values);
            IEnumerable<DiscordMessageBuilder> helpMessages = FormatHelpMessage(commandCategories, args);

            foreach(var helpMessage in helpMessages) {
                helpMessage.AddComponents(buttons);
            }

            return helpMessages.ToList()[args.CategoryIndex];
        }
    }
}
