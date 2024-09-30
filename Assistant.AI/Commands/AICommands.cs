﻿using AssistantAI.ContextChecks;
using AssistantAI.DataTypes;
using AssistantAI.Services;
using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities.Extension;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using System.ComponentModel;

namespace AssistantAI.Commands;

[Command("ai")]
[Description("Commands for the AI.")]
public class AICommands {

    [Command("blacklist-user")]
    [Description("Blacklist a user from using the AI.")]
    [RequirePermissions(DiscordPermissions.None, DiscordPermissions.ManageMessages)]
    [RequireGuild()]
    [Cooldown(5)]
    public static ValueTask BlacklistUser(CommandContext ctx, DiscordUser user) {
        if(user.IsBot)
            return ctx.ResponeTryEphemeral("You can't blacklist a bot.", true);

        IDatabaseService<Data> database = ServiceManager.GetService<IDatabaseService<Data>>();
        database.Data.GetOrDefaultGuild(ctx.Guild!.Id).GetOrDefaultGuildUser(user.Id).BlacklistStatus = AIResponsePermission.Blacklisted;

        database.SaveDatabase();


        return ctx.ResponeTryEphemeral($"Blacklisted {user.Mention}.", true);
    }

    [Command("unblacklist-user")]
    [Description("Remove a user from the blacklist.")]
    [RequirePermissions(DiscordPermissions.None, DiscordPermissions.ManageMessages)]
    [RequireGuild()]
    [Cooldown(5)]
    public static ValueTask UnBlacklistUser(CommandContext ctx, DiscordUser user) {
        if(user.IsBot)
            return ctx.ResponeTryEphemeral("You can't blacklist a bot.", true);

        IDatabaseService<Data> database = ServiceManager.GetService<IDatabaseService<Data>>();

        database.Data.GetOrDefaultGuild(ctx.Guild!.Id).GetOrDefaultGuildUser(user.Id).BlacklistStatus = AIResponsePermission.None;
        database.SaveDatabase();

        return ctx.ResponeTryEphemeral($"Whitelisted {user.Mention}.", true);
    }

    [Command("ignore-me")]
    [Description("'true' to be ignored by the bot, and 'false' to remove yourself from the ignore list.")]
    [RequireGuild()]
    [Cooldown(5)]
    public static ValueTask IgnoreMe(CommandContext ctx, bool ignore = true) {
        IDatabaseService<Data> database = ServiceManager.GetService<IDatabaseService<Data>>();

        if(ignore && database.Data.GetOrDefaultGuild(ctx.Guild!.Id).GetOrDefaultGuildUser(ctx.User.Id).BlacklistStatus == AIResponsePermission.Blacklisted)
            return ctx.ResponeTryEphemeral("You can't ignore yourself if you're blacklisted.", true);

        if(ignore)
            database.Data.GetOrDefaultGuild(ctx.Guild!.Id).GetOrDefaultGuildUser(ctx.User.Id).BlacklistStatus = AIResponsePermission.Ignored;
        else
            database.Data.GetOrDefaultGuild(ctx.Guild!.Id).GetOrDefaultGuildUser(ctx.User.Id).BlacklistStatus = AIResponsePermission.None;

        database.SaveDatabase();

        return ctx.ResponeTryEphemeral($"AI will {(ignore ? "now" : "no longer")} ignored you.", true);
    }
}
