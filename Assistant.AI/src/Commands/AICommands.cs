using AssistantAI.ContextChecks;
using AssistantAI.DataTypes;
using AssistantAI.Services;
using AssistantAI.Utilities.Extension;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel;

namespace AssistantAI.Commands;

[Command("ai")]
[Description("Commands for the AI.")]
public class AICommands {
    [Command("blacklist-user")]
    [Description("Blacklist or unblacklist a user from using the AI.")]
    [RequirePermissions(DiscordPermissions.None, DiscordPermissions.ManageMessages)]
    [RequireGuild()]
    [Cooldown(5)]
    public static ValueTask BlacklistUser(CommandContext ctx, DiscordUser user, bool blacklisted = true) {
        SqliteDatabaseContext database = ServiceManager.GetService<SqliteDatabaseContext>();

        UserData? userData = database.UserDataSet
            .FirstOrDefault(u => (ulong)u.UserId == user.Id);

        GuildData? guildData = database.GuildDataSet
            .Include(g => g.GuildUsers)
            .FirstOrDefault(g => (ulong)g.GuildId == ctx.Guild!.Id);

        GuildUserData? guildUserData = guildData?.GuildUsers
            .FirstOrDefault(u => (ulong)u.GuildUserId == user.Id);

        bool guildExists = guildData != null;
        bool guildUserExists = guildUserData != null;

        userData ??= new UserData {
            UserId = (long)user.Id
        };

        guildData ??= new GuildData {
            GuildId = (long)ctx.Guild!.Id,
            GuildUsers = []
        };

        guildUserData ??= new GuildUserData {
            GuildUserId = (long)user.Id,
            GuildDataId = guildData.GuildId,
        };


        if(user.IsBot)
            return ctx.ResponeTryEphemeral("You can't blacklist a bot.", true);
        else if(userData.ResponsePermission == AIResponsePermission.Blacklisted)
            return ctx.ResponeTryEphemeral("You can't blacklist a user that is globally blacklisted.", true);
        else if (guildUserData.ResponsePermission == (blacklisted ? AIResponsePermission.Blacklisted : AIResponsePermission.None))
            return ctx.ResponeTryEphemeral($"{user.Mention} is already {(blacklisted ? "blacklisted" : "unblacklisted")} from using the AI.", true);

        guildUserData.ResponsePermission = blacklisted ? AIResponsePermission.Blacklisted : AIResponsePermission.None;
        if(!guildExists) {
            database.Entry(guildData).State = EntityState.Added;
        } else if(!guildUserExists) {
            database.Entry(guildUserData).State = EntityState.Added;
            guildData.GuildUsers.Add(guildUserData);
        } else {
            database.Entry(guildUserData).State = EntityState.Modified;
        }



        database.SaveChanges();

        return ctx.ResponeTryEphemeral($"{(blacklisted ? "Blacklisted" : "Unblacklisted")} {user.Mention} from using the AI.", true);
    }

    [Command("ignore-me")]
    [Description("'true' to be ignored by the bot, and 'false' to remove yourself from the ignore list.")]
    [RequireGuild()]
    [Cooldown(5)]
    public static ValueTask IgnoreMe(CommandContext ctx, bool ignore = true) {
        SqliteDatabaseContext database = ServiceManager.GetService<SqliteDatabaseContext>();

        UserData? userData = database.UserDataSet
            .FirstOrDefault(u => (ulong)u.UserId == ctx.User.Id);

        GuildData? guildData = database.GuildDataSet
            .Include(g => g.GuildUsers)
            .FirstOrDefault(g => (ulong)g.GuildId == ctx.Guild!.Id);

        GuildUserData? guildUserData = guildData?.GuildUsers
            .FirstOrDefault(u => (ulong)u.GuildUserId == ctx.User.Id);

        bool guildExists = guildData != null;
        bool guildUserExists = guildUserData != null;

        userData ??= new UserData {
            UserId = (long)ctx.User.Id
        };

        guildData ??= new GuildData {
            GuildId = (long)ctx.Guild!.Id,
            GuildUsers = []
        };

        guildUserData ??= new GuildUserData {
            GuildUserId = (long)ctx.User.Id,
            GuildDataId = guildData.GuildId,
        };


        if(guildUserData.ResponsePermission == AIResponsePermission.Blacklisted)
            return ctx.ResponeTryEphemeral("You can't ignore yourself if you are blacklisted.", true);
        else if(userData.ResponsePermission == AIResponsePermission.Blacklisted)
            return ctx.ResponeTryEphemeral("You can't ignore yourself if you are globally blacklisted.", true);
        else if(guildUserData.ResponsePermission == (ignore ? AIResponsePermission.Ignored : AIResponsePermission.None))
            return ctx.ResponeTryEphemeral($"You are already {(ignore ? "ignored" : "not ignored")} by the bot.", true);

        guildUserData.ResponsePermission = ignore ? AIResponsePermission.Ignored : AIResponsePermission.None;
        if(!guildExists) {
            database.Entry(guildData).State = EntityState.Added;
        } else if(!guildUserExists) {
            database.Entry(guildUserData).State = EntityState.Added;
            guildData.GuildUsers.Add(guildUserData);
        } else {
            database.Entry(guildUserData).State = EntityState.Modified;
        }

        database.SaveChanges();

        return ctx.ResponeTryEphemeral($"AI will {(ignore ? "now" : "no longer")} ignored you.", true);
    }

    [Command("global-blacklist-user")]
    [Description("Globally blacklist or unblacklist a user.")]
    [RequireApplicationOwner()]
    public static ValueTask GlobalBlacklistUser(CommandContext ctx, DiscordUser user, bool blacklisted = true) {
        SqliteDatabaseContext database = ServiceManager.GetService<SqliteDatabaseContext>();

        UserData? userData = database.UserDataSet
            .FirstOrDefault(u => (ulong)u.UserId == user.Id);


        bool userExists = userData != null;

        userData ??= new UserData {
            UserId = (long)user.Id
        };

        if(user.IsBot)
            return ctx.ResponeTryEphemeral("You can't blacklist a bot.", true);

        userData.ResponsePermission = blacklisted ? AIResponsePermission.Blacklisted : AIResponsePermission.None;
        if(!userExists)
            database.UserDataSet.Add(userData);

        database.SaveChanges();

        return ctx.ResponeTryEphemeral($"Globally {(blacklisted ? "blacklisted" : "unblacklisted")} {user.Mention}.", true);
    }
}
