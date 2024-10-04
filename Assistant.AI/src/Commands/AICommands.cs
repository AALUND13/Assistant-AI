using AssistantAI.ContextChecks;
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
        IDatabaseService database = ServiceManager.GetService<IDatabaseService>();

        if(user.IsBot)
            return ctx.ResponeTryEphemeral("You can't blacklist a bot.", true);
        else if(database.Data.GetOrDefaultUser(user.Id).ResponsePermission == AIResponsePermission.Blacklisted)
            return ctx.ResponeTryEphemeral("You can't blacklist a user that is globally blacklisted.", true);

        database.Data.GetOrDefaultGuild(ctx.Guild!.Id).GetOrDefaultGuildUser(user.Id).ResponsePermission = AIResponsePermission.Blacklisted;
        database.SaveDatabase();

        return ctx.ResponeTryEphemeral($"Blacklisted {user.Mention}.", true);
    }

    [Command("unblacklist-user")]
    [Description("Remove a user from the blacklist.")]
    [RequirePermissions(DiscordPermissions.None, DiscordPermissions.ManageMessages)]
    [RequireGuild()]
    [Cooldown(5)]
    public static ValueTask UnBlacklistUser(CommandContext ctx, DiscordUser user) {
        IDatabaseService database = ServiceManager.GetService<IDatabaseService>();

        if(user.IsBot)
            return ctx.ResponeTryEphemeral("You can't blacklist a bot.", true);
        else if(database.Data.GetOrDefaultUser(user.Id).ResponsePermission == AIResponsePermission.Blacklisted)
            return ctx.ResponeTryEphemeral("You can't unblacklist a user that is globally blacklisted.", true);

        database.Data.GetOrDefaultGuild(ctx.Guild!.Id).GetOrDefaultGuildUser(user.Id).ResponsePermission = AIResponsePermission.None;
        database.SaveDatabase();

        return ctx.ResponeTryEphemeral($"Whitelisted {user.Mention}.", true);
    }

    [Command("ignore-me")]
    [Description("'true' to be ignored by the bot, and 'false' to remove yourself from the ignore list.")]
    [RequireGuild()]
    [Cooldown(5)]
    public static ValueTask IgnoreMe(CommandContext ctx, bool ignore = true) {
        IDatabaseService database = ServiceManager.GetService<IDatabaseService>();

        if(ignore && database.Data.GetOrDefaultGuild(ctx.Guild!.Id).GetOrDefaultGuildUser(ctx.User.Id).ResponsePermission == AIResponsePermission.Blacklisted)
            return ctx.ResponeTryEphemeral("You can't ignore yourself if you are blacklisted.", true);
        else if(database.Data.GetOrDefaultUser(ctx.User.Id).ResponsePermission == AIResponsePermission.Blacklisted)
            return ctx.ResponeTryEphemeral("You can't ignore yourself if you are globally blacklisted.", true);

        if(ignore)
            database.Data.GetOrDefaultGuild(ctx.Guild!.Id).GetOrDefaultGuildUser(ctx.User.Id).ResponsePermission = AIResponsePermission.Ignored;
        else
            database.Data.GetOrDefaultGuild(ctx.Guild!.Id).GetOrDefaultGuildUser(ctx.User.Id).ResponsePermission = AIResponsePermission.None;

        database.SaveDatabase();

        return ctx.ResponeTryEphemeral($"AI will {(ignore ? "now" : "no longer")} ignored you.", true);
    }

    [Command("global-blacklist-user")]
    [Description("Globally blacklist or unblacklist a user.")]
    [RequireApplicationOwner()]
    public static ValueTask GlobalBlacklistUser(CommandContext ctx, DiscordUser user, bool blacklisted = true) {
        IDatabaseService database = ServiceManager.GetService<IDatabaseService>();

        if(user.IsBot)
            return ctx.ResponeTryEphemeral("You can't blacklist a bot.", true);

        database.Data.GetOrDefaultUser(user.Id).ResponsePermission = blacklisted ? AIResponsePermission.Blacklisted : AIResponsePermission.None;
        database.SaveDatabase();

        return ctx.ResponeTryEphemeral($"Globally {(blacklisted ? "blacklisted" : "unblacklisted")} {user.Mention}.", true);
    }

}
