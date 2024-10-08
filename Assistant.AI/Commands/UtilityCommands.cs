﻿using AssistantAI.ContextChecks;
using AssistantAI.Utilities.Extensions;
using DSharpPlus.Commands;
using System.ComponentModel;

namespace AssistantAI.Commands;

public class UtilityCommands {
    [Command("ping")]
    [Description("Get the bot's latency.")]
    [Cooldown(5)]
    public static ValueTask PingAsync(CommandContext ctx) {
        return ctx.ResponeTryEphemeral($"Pong! **{(int)ctx.Client.GetConnectionLatency(ctx.Guild?.Id ?? ctx.Client.Guilds[0].Id).TotalMilliseconds}ms**", true);
    }
}