using AssistantAI.ContextChecks;
using AssistantAI.Utilities.Extension;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using System.ComponentModel;

namespace AssistantAI.Commands;

public class Utility {
    [Command("ping")]
    [Description("Get the bot's latency.")]
    [Cooldown(5)]
    public static ValueTask PingAsync(CommandContext ctx) {
        return ctx.ResponeTryEphemeral($"Pong! **{(int)ctx.Client.GetConnectionLatency(ctx.Guild?.Id ?? ctx.Client.Guilds[0].Id).TotalMilliseconds}ms**", true);
    }
}