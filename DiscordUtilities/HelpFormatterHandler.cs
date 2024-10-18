using AssistantAI.DiscordUtilities.EventArgs;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System.Collections.Concurrent;

namespace AssistantAI.DiscordUtilities {
    public class HelpFormatterHandler : IEventHandler<ComponentInteractionCreatedEventArgs> {
        private static readonly ConcurrentDictionary<ulong, int> categoryIndex = [];

        private readonly BaseHelpFormatter helpFormatter;
        private readonly CommandsExtension commandsExtension;

        public HelpFormatterHandler(BaseHelpFormatter helpFormatter, CommandsExtension commandsExtension) {
            this.helpFormatter = helpFormatter;
            this.commandsExtension = commandsExtension;
        }

        public async Task HandleEventAsync(DiscordClient sender, ComponentInteractionCreatedEventArgs eventArgs) {
            int categoryLength = (await helpFormatter.GetCommandCategories(commandsExtension.Commands.Values)).Count;
            int currentIndex = categoryIndex.GetOrAdd(eventArgs.Guild.Id, 0);

            if(eventArgs.Id == "previous-category") {
                categoryIndex[eventArgs.Guild.Id] = currentIndex == 0 ? categoryLength - 1 : currentIndex - 1;
            } else if(eventArgs.Id == "next-category") {
                categoryIndex[eventArgs.Guild.Id] = currentIndex == categoryLength - 1 ? 0 : currentIndex + 1;
            } else {
                return;
            }

            HelpFormatterEventArgs args = new() {
                Guild = eventArgs.Guild,
                Channel = eventArgs.Channel,
                User = eventArgs.User,

                CommandsExtension = commandsExtension,
                CategoryIndex = categoryIndex[eventArgs.Guild.Id]
            };

            DiscordMessageBuilder helpMessage = await helpFormatter.BuildHelpMessages(commandsExtension, args);
            await eventArgs.Interaction.CreateResponseAsync(
                DiscordInteractionResponseType.UpdateMessage,
                new DiscordInteractionResponseBuilder(helpMessage)
            );
        }
    }
}
