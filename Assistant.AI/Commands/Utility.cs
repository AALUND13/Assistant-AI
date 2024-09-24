using AssistantAI.ContextChecks;
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
        if(ctx is SlashCommandContext slashContext) {
            return slashContext.RespondAsync($"Pong! **{(int)ctx.Client.GetConnectionLatency(ctx.Guild?.Id ?? ctx.Client.Guilds[0].Id).TotalMilliseconds}ms**", true);
        } else {
            return ctx.RespondAsync($"Pong! **{(int)ctx.Client.GetConnectionLatency(ctx.Guild?.Id ?? ctx.Client.Guilds[0].Id).TotalMilliseconds}ms**");
        }
    }

#if DEBUG
    [Command("join-vc")]
    [Description("Join the voice channel you are in.")]
    [Cooldown(5)]
    public async ValueTask JoinVcAsync(SlashCommandContext ctx, DiscordChannel? channel = null) {
        channel ??= ctx.Member!.VoiceState?.Channel;
        await channel.ConnectAsync();
        await ctx.RespondAsync($"Joined {channel.Mention}.");
    }
#endif
}