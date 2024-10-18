using AssistantAI.DiscordUtilities.EventArgs;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;

namespace AssistantAI.DiscordUtilities;

public abstract class BaseHelpFormatter {
    private static DiscordButtonComponent[] buttons = [
        new DiscordButtonComponent(DiscordButtonStyle.Primary, "previous-category", "Previous Category"),
        new DiscordButtonComponent(DiscordButtonStyle.Primary, "next-category", "Next Category")
    ];

    public abstract Task<IEnumerable<DiscordMessageBuilder>> FormatHelpMessage(Dictionary<string, IEnumerable<Command>> commandCategories, HelpFormatterEventArgs args);
    public abstract Task<Dictionary<string, IEnumerable<Command>>> GetCommandCategories(IEnumerable<Command> commands);

    public async Task<DiscordMessageBuilder> BuildHelpMessages(CommandsExtension commandsExtension, HelpFormatterEventArgs args) {
        Dictionary<string, IEnumerable<Command>> commandCategories = await GetCommandCategories(commandsExtension.Commands.Values);
        IEnumerable<DiscordMessageBuilder> helpMessages = await FormatHelpMessage(commandCategories, args);

        if(commandCategories.Count == 1) {
            return helpMessages.First();
        } else {
            foreach(var helpMessage in helpMessages) {
                helpMessage.AddComponents(buttons);
            }

            return helpMessages.ToList()[args.CategoryIndex];
        }
    }
}
