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

#if DEBUG
    [Command("join-vc")]
    [Description("Join the voice channel you are in.")]
    [Cooldown(5)]
    public async ValueTask JoinVcAsync(SlashCommandContext ctx, DiscordChannel? channel = null) {
        channel ??= ctx.Member!.VoiceState?.Channel;
        await channel!.ConnectAsync();
        await ctx.ResponeTryEphemeral($"Joined {channel!.Mention}.", true);
    }
#endif
}