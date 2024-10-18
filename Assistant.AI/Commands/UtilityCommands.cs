using AssistantAI.ContextChecks;
using AssistantAI.DiscordUtilities;
using AssistantAI.Services;
using AssistantAI.Utilities.Extensions;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.TextCommands;
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
        DiscordMessageBuilder helpMessage = await ServiceManager.GetService<BaseHelpFormatter>().BuildHelpMessages(ctx.Extension, new() {
            User = ctx.User,
            Guild = ctx.Guild,
            Channel = ctx.Channel,
            CommandsExtension = ctx.Extension
        });

        await ctx.ResponeTryEphemeral(helpMessage, true);
    }
}