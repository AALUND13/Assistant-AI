﻿using AssistantAI.ContextChecks;
using AssistantAI.Services;
using AssistantAI.Utilities.Extensions;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace AssistantAI.Commands;

[Command("ai")]
[Description("Commands for the AI.")]
public class AICommands {
    [Command("ignore"), RequireGuild()]
    [Description("'true' to be ignored by the bot, and 'false' to remove yourself from the ignore list.")]
    [Cooldown(5)]
    public static async ValueTask IgnoreMe(CommandContext ctx) {
        using var scope = ServiceManager.ServiceProvider!.CreateScope();
        SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

        UserData? userData = await databaseContent.UserDataSet
            .FirstOrDefaultAsync(u => (ulong)u.UserId == ctx.User.Id);

        GuildData? guildData = await databaseContent.GuildDataSet
            .Include(g => g.GuildUsers)
            .FirstOrDefaultAsync(g => (ulong)g.GuildId == ctx.Guild!.Id);

        GuildUserData? guildUserData = guildData?.GuildUsers
            .FirstOrDefault(u => (ulong)u.GuildUserId == ctx.User.Id);

        bool guildExists = guildData != null;
        bool guildUserExists = guildUserData != null;

        userData ??= new UserData {
            UserId = ctx.User.Id
        };

        guildData ??= new GuildData {
            GuildId = ctx.Guild!.Id,
            GuildUsers = []
        };

        guildUserData ??= new GuildUserData {
            GuildUserId = ctx.User.Id,
            GuildDataId = guildData.GuildId,
        };

        bool ignore = guildUserData.ResponsePermission != AIResponsePermission.Ignored;
        if(guildUserData.ResponsePermission == AIResponsePermission.Blacklisted) {
            await ctx.ResponeTryEphemeral("You can't ignore yourself if you are blacklisted.", true);
            return;
        } else if(userData.ResponsePermission == AIResponsePermission.Blacklisted) {
            await ctx.ResponeTryEphemeral("You can't ignore yourself if you are globally blacklisted.", true);
            return;
        } else if(guildUserData.ResponsePermission == (ignore ? AIResponsePermission.Ignored : AIResponsePermission.None)) {
            await ctx.ResponeTryEphemeral($"You are already {(ignore ? "ignored" : "not ignored")} by the bot.", true);
            return;
        }

        guildUserData.ResponsePermission = ignore ? AIResponsePermission.Ignored : AIResponsePermission.None;
        if(!guildExists) {
            databaseContent.Entry(guildData).State = EntityState.Added;
        } else if(!guildUserExists) {
            databaseContent.Entry(guildUserData).State = EntityState.Added;
            guildData.GuildUsers.Add(guildUserData);
        } else {
            databaseContent.Entry(guildUserData).State = EntityState.Modified;
        }

        databaseContent.SaveChanges();

        await ctx.ResponeTryEphemeral($"AI will {(ignore ? "now" : "no longer")} ignored you.", true);
    }

    [Command("global-blacklist-user"), RequireApplicationOwner()]
    [Description("Globally blacklist or unblacklist a user.")]
    public static async ValueTask GlobalBlacklistUser(CommandContext ctx, DiscordUser user, bool blacklisted = true) {
        using var scope = ServiceManager.ServiceProvider!.CreateScope();
        SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

        UserData? userData = databaseContent.UserDataSet
            .FirstOrDefault(u => u.UserId == user.Id);

        bool userExists = userData != null;
        userData ??= new UserData {
            UserId = user.Id
        };

        if(user.IsBot) {
            await ctx.ResponeTryEphemeral("You can't blacklist a bot.", true);
            return;
        }

        userData.ResponsePermission = blacklisted ? AIResponsePermission.Blacklisted : AIResponsePermission.None;
        if(!userExists) {
            await databaseContent.UserDataSet.AddAsync(userData);
        }
        databaseContent.SaveChanges();

        await ctx.ResponeTryEphemeral($"Globally {(blacklisted ? "blacklisted" : "unblacklisted")} {user.Mention}.", true);
    }
}
