using AssistantAI.DiscordUtilities.EventArgs;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System.Collections.Concurrent;

namespace AssistantAI.DiscordUtilities {
    public class HelpFormatterHandler : IEventHandler<ComponentInteractionCreatedEventArgs> {
        private readonly ConcurrentDictionary<ulong, int> categoryIndex = [];

        private readonly BaseHelpFormatter helpFormatter;
        private readonly CommandsExtension commandsExtension;

        public HelpFormatterHandler(BaseHelpFormatter helpFormatter, CommandsExtension commandsExtension) {
            this.helpFormatter = helpFormatter;
            this.commandsExtension = commandsExtension;
        }

        public async Task HandleEventAsync(DiscordClient sender, ComponentInteractionCreatedEventArgs eventArgs) {
            int categoryLength = helpFormatter.GetCommandCategories(commandsExtension.Commands.Values).Count;
            if(eventArgs.Id == "previous-category") {
                categoryIndex.AddOrUpdate(eventArgs.Guild.Id, 0, (key, value) => value == 0 ? categoryLength - 1 : value - 1);
            } else if(eventArgs.Id == "next-category") {
                categoryIndex.AddOrUpdate(eventArgs.Guild.Id, 0, (key, value) => value == categoryLength - 1 ? 0 : value + 1);
            } else {
                return;
            }

            HelpFormatterEventArgs args = new() {
                Guild = eventArgs.Guild,
                Channel = eventArgs.Channel,
                Message = eventArgs.Message,
                CommandsExtension = commandsExtension,
                CategoryIndex = categoryIndex[eventArgs.Guild.Id]
            };

            DiscordMessageBuilder helpMessage = helpFormatter.BuildHelpMessages(commandsExtension, args);
            await eventArgs.Interaction.CreateResponseAsync(
                DiscordInteractionResponseType.UpdateMessage,
                new DiscordInteractionResponseBuilder(helpMessage)
            );
        }
    }
}
