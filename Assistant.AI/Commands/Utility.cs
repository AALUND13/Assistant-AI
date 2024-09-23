using AssistantAI.ContextChecks;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using System.ComponentModel;

namespace AssistantAI.Commands {
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
    }
}