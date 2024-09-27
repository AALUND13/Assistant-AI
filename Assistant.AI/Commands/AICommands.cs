using AssistantAI.ContextChecks;
using AssistantAI.Services;
using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities.Extension;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Entities;
using System.ComponentModel;

namespace AssistantAI.Commands {
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
                return ctx.RespondAsync("You can't blacklist a bot.");

            IDatabaseService<Data> database = ServiceManager.GetService<IDatabaseService<Data>>();
            database.Data.TryAddGuildData(ctx.Guild!.Id);

            database.Data.GuildData[ctx.Guild!.Id].BlacklistedUsers.Add((user.Id, BlacklistStatus.Blacklisted));
            database.SaveDatabase();


            return ctx.ResponeTryEphemeral($"Blacklisted {user.Mention}.", true);
        }

        [Command("whitelist-user")]
        [Description("Remove a user from the blacklist.")]
        [RequirePermissions(DiscordPermissions.None, DiscordPermissions.ManageMessages)]
        [RequireGuild()]
        [Cooldown(5)]
        public static ValueTask WhitelistUser(CommandContext ctx, DiscordUser user) {
            if(user.IsBot)
                return ctx.RespondAsync("You can't blacklist a bot.");

            IDatabaseService<Data> database = ServiceManager.GetService<IDatabaseService<Data>>();
            database.Data.TryAddGuildData(ctx.Guild!.Id);

            database.Data.GuildData[ctx.Guild!.Id].BlacklistedUsers.RemoveAll(user => user.userID == ctx.User.Id);
            database.SaveDatabase();

            return ctx.ResponeTryEphemeral($"Whitelisted {user.Mention}.", true);
        }

        [Command("ignore-me")]
        [Description("'true' to be ignored by the bot, and 'false' to remove yourself from the ignore list.")]
        [RequireGuild()]
        [Cooldown(5)]
        public static ValueTask IgnoreMe(CommandContext ctx, bool ignore = true) {
            IDatabaseService<Data> database = ServiceManager.GetService<IDatabaseService<Data>>();
            database.Data.TryAddGuildData(ctx.Guild!.Id);

            if(ignore && database.Data.GuildData[ctx.Guild!.Id].BlacklistedUsers.Any(user => user.userID == ctx.User.Id))
                return ctx.ResponeTryEphemeral("You can't ignore yourself if you're blacklisted.", true);

            if(ignore)
                database.Data.GuildData[ctx.Guild!.Id].BlacklistedUsers.Add((ctx.User.Id, BlacklistStatus.Ingored));
            else
                database.Data.GuildData[ctx.Guild!.Id].BlacklistedUsers.RemoveAll(user => user.userID == ctx.User.Id);

            database.SaveDatabase();

            return ctx.ResponeTryEphemeral($"AI will {(ignore ? "now" : "no longer")} ignored you.", true);
        }
    }
}
