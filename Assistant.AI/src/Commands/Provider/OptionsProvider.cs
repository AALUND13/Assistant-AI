using AssistantAI.Attributes;
using AssistantAI.DataTypes;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Commands.Trees;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace AssistantAI.Commands.Provider {
    internal class OptionsProvider : IChoiceProvider {
        public ValueTask<IReadOnlyDictionary<string, object>> ProvideAsync(CommandParameter parameter) {
            string[] optionNames = typeof(GuildOptions)
                .GetProperties()
                .Where(x => x.GetCustomAttribute<IgnoreAttribute>() == null)
                .Select(x => x.Name)
                .ToArray();

            var options = new Dictionary<string, object>();

            for(int i = 0; i < optionNames.Length; i++) {
                options.Add(optionNames[i], optionNames[i]);
            }

            return new ValueTask<IReadOnlyDictionary<string, object>>(options);
        }
    }
}
