using AssistantAI.DataTypes;
using AssistantAI.Services;
using AssistantAI.Services.Interfaces;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Entities;

namespace AssistantAI.Commands.Parsing;

public class GuildPrefixResolver : IPrefixResolver {

    public string Prefix { get; init; }

    /// <summary>
    /// Guild prefix resolver.
    /// </summary>
    /// <param name="prefix">The prefix to default to if no guild prefix is found.</param>
    public GuildPrefixResolver(string prefix) {
        if(string.IsNullOrWhiteSpace(prefix)) {
            throw new ArgumentException("Prefix must not be null or empty.", nameof(prefix));
        }

        Prefix = prefix;
    }

    public ValueTask<int> ResolvePrefixAsync(CommandsExtension extension, DiscordMessage message) {
        if(message.Channel!.IsPrivate) {
            if(message.Content.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) {
                return ValueTask.FromResult(Prefix.Length);
            }
        } else {
            IDatabaseService<Data> databaseService = ServiceManager.GetService<IDatabaseService<Data>>();
            GuildData guildData = databaseService.Data.GetOrDefaultGuild(message.Channel.Guild.Id);

            if(string.IsNullOrWhiteSpace(guildData.Options.Prefix)) {
                return ValueTask.FromResult(-1);
            } else if(message.Content.StartsWith(guildData.Options.Prefix, StringComparison.OrdinalIgnoreCase)) {
                return ValueTask.FromResult(guildData.Options.Prefix.Length);
            }
        }

        return ValueTask.FromResult(-1);
    }
}
