using AssistantAI.ContextChecks;
using AssistantAI.Services;
using AssistantAI.Utilities.Extensions;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Trees;
using DSharpPlus.Entities;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Text;

namespace AssistantAI.Commands;

public class UtilityCommands {
    [Command("ping")]
    [Description("Get the bot's latency.")]
    [Cooldown(5)]
    public static ValueTask PingAsync(CommandContext ctx) {
        return ctx.ResponeTryEphemeral($"Pong! **{(int)ctx.Client.GetConnectionLatency(ctx.Guild?.Id ?? ctx.Client.Guilds[0].Id).TotalMilliseconds}ms**", true);
    }

    [Command("help")]
    [Description("Get a list of all the commands.")]
    [Cooldown(5)]
    public static async ValueTask HelpAsync(CommandContext ctx) {
        using var scope = ServiceManager.ServiceProvider!.CreateScope();
        SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

        ulong guildId = ctx.Guild?.Id ?? 0;
        GuildData? guildData = databaseContent.GuildDataSet.FirstOrDefault(guild => guild.GuildId == guildId);
        guildData ??= new GuildData();

        List<Command> commands = GetCommands(ctx.Extension);
        StringBuilder stringBuilder = new();

        int lastTabSpaces = 0;
        foreach(var command in commands) {
            int tabSpaces = command.FullName.Split(" ").Length - 1;
            if(tabSpaces < lastTabSpaces)
                stringBuilder.AppendLine();

            stringBuilder.AppendLine($"{new string('\u3000', tabSpaces)}**{guildData.Options.Prefix}{command.FullName}**: {command.Description}");

            lastTabSpaces = tabSpaces;
        }

        DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder {
            Title = "Commands",
            Description = stringBuilder.ToString(),
            Color = DiscordColor.Gold
        };

        await ctx.ResponeTryEphemeral(embedBuilder, true);
    }

    private static List<Command> GetCommands(CommandsExtension commandsExtension) {
        List<Command> commands = [];

        foreach(var command in commandsExtension.Commands) {
            commands.Add(command.Value);

            if(command.Value.Subcommands.Count != 0)
                commands.AddRange(GetSubCommands(command.Value));

        }

        return commands;
    }

    private static List<Command> GetSubCommands(Command commandGroup) {
        List<Command> commands = [];

        foreach(var command in commandGroup.Subcommands) {
            commands.Add(command);

            if(command.Subcommands.Count != 0)
                commands.AddRange(GetSubCommands(commandGroup));

        }

        return commands;
    }
}