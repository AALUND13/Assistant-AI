using AssistantAI.AiModule.Attributes;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using System.Reflection;

namespace AssistantAI.Commands.Provider
{
    internal class OptionsProvider : IChoiceProvider {
        public ValueTask<IEnumerable<DiscordApplicationCommandOptionChoice>> ProvideAsync(CommandParameter parameter) {
            string[] optionNames = typeof(GuildOptions)
                .GetProperties()
                .Where(x => x.GetCustomAttribute<IgnoreAttribute>() == null)
                .Select(x => x.Name)
                .ToArray();


            return ValueTask.FromResult(optionNames.Select(optionName => new DiscordApplicationCommandOptionChoice(optionName, optionName)));
        }
    }
}
