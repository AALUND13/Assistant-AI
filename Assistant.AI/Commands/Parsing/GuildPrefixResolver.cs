using AssistantAI.Services;
using AssistantAI.Services.Interfaces;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;

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
            using var scope = ServiceManager.ServiceProvider!.CreateScope();
            SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

            GuildData? guildData = databaseContent.GuildDataSet.FirstOrDefault(guild => (ulong)guild.GuildId == message.Channel.Guild.Id);
            guildData ??= new GuildData();

            if(string.IsNullOrWhiteSpace(guildData.Options.Prefix)) {
                return ValueTask.FromResult(-1);
            } else if(message.Content.StartsWith(guildData.Options.Prefix, StringComparison.OrdinalIgnoreCase)) {
                return ValueTask.FromResult(guildData.Options.Prefix.Length);
            }
        }

        return ValueTask.FromResult(-1);
    }
}
