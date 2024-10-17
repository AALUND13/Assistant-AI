using AssistantAI.AiModule.Attributes;
using AssistantAI.Commands.Provider;
using AssistantAI.ContextChecks;
using AssistantAI.Services;
using AssistantAI.Utilities;
using AssistantAI.Utilities.Extensions;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace AssistantAI.Commands;

[Command("admin"), RequirePermissions(DiscordPermissions.None, DiscordPermissions.ManageGuild)]
[Description("Commands for the administrators.")]
class AdminCommand {
    [Command("blacklist-user"), RequireGuild()]
    [Description("Blacklist or unblacklist a user from using the AI.")]
    [Cooldown(5)]
    public static async ValueTask BlacklistUser(CommandContext ctx, DiscordUser user, bool blacklisted = true) {
        using var scope = ServiceManager.ServiceProvider!.CreateScope();
        SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

        UserData? userData = await databaseContent.UserDataSet
            .FirstOrDefaultAsync(u => u.UserId == user.Id);

        GuildData? guildData = await databaseContent.GuildDataSet
            .Include(g => g.GuildUsers)
            .FirstOrDefaultAsync(g => g.GuildId == ctx.Guild!.Id);

        GuildUserData? guildUserData = guildData?.GuildUsers
            .FirstOrDefault(u => u.GuildUserId == user.Id);

        bool guildExists = guildData != null;
        bool guildUserExists = guildUserData != null;

        userData ??= new UserData {
            UserId = user.Id
        };

        guildData ??= new GuildData {
            GuildId = ctx.Guild!.Id,
            GuildUsers = []
        };

        guildUserData ??= new GuildUserData {
            GuildUserId = user.Id,
            GuildDataId = guildData.GuildId,
        };


        if(user.IsBot) {
            await ctx.ResponeTryEphemeral("You can't blacklist a bot.", true);
            return;
        } else if(userData.ResponsePermission == AIResponsePermission.Blacklisted) {
            await ctx.ResponeTryEphemeral("You can't blacklist a user that is globally blacklisted.", true);
            return;
        } else if(guildUserData.ResponsePermission == (blacklisted ? AIResponsePermission.Blacklisted : AIResponsePermission.None)) {
            await ctx.ResponeTryEphemeral($"{user.Mention} is already {(blacklisted ? "blacklisted" : "unblacklisted")} from using the AI.", true);
            return;
        }

        guildUserData.ResponsePermission = blacklisted ? AIResponsePermission.Blacklisted : AIResponsePermission.None;
        if(!guildExists) {
            databaseContent.Entry(guildData).State = EntityState.Added;
        } else if(!guildUserExists) {
            databaseContent.Entry(guildUserData).State = EntityState.Added;
            guildData.GuildUsers.Add(guildUserData);
        } else {
            databaseContent.Entry(guildUserData).State = EntityState.Modified;
        }



        databaseContent.SaveChanges();

        await ctx.ResponeTryEphemeral($"{(blacklisted ? "Blacklisted" : "Unblacklisted")} {user.Mention} from using the AI.", true);
    }

    [Command("option-get"), RequireGuild()]
    [Description("Get the current options for the guild.")]
    [Cooldown(5)]
    public static async ValueTask GetOptions(CommandContext ctx) {
        using var scope = ServiceManager.ServiceProvider!.CreateScope();
        SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();


        GuildData? guildData = await databaseContent.GuildDataSet
            .Include(g => g.Options)
            .ThenInclude(g => g.ChannelWhitelists)
            .FirstOrDefaultAsync(g => g.GuildId == ctx.Guild!.Id);

        guildData ??= new GuildData() {
            GuildId = ctx.Guild!.Id,
        };

        object?[] options = typeof(GuildOptions)
            .GetProperties()
            .Where(x => x.GetCustomAttribute<IgnoreAttribute>() == null)
            .Select(x => x.GetValue(guildData.Options))
            .ToArray();

        string[] optionNames = typeof(GuildOptions)
            .GetProperties()
            .Where(x => x.GetCustomAttribute<IgnoreAttribute>() == null)
            .Select(x => x.Name)
            .ToArray();

        var embed = new DiscordEmbedBuilder()
            .WithTitle($"Options [{ctx.Guild!.Name}]")
            .WithColor(DiscordColor.Gold);

        var optionsBuilder = new StringBuilder();

        for(int i = 0; i < options.Length; i++) {
            Type? optionType = options[i]?.GetType();
            if(optionType == null) {
                optionsBuilder.AppendLine($"**{optionNames[i]}**: `Null`");
                continue;
            } else {
                MethodInfo? previewMethod = typeof(PreviewManager)
                    .GetMethod("GetPreview")
                    ?.MakeGenericMethod(optionType);

                var previewInstance = previewMethod?.Invoke(null, null)!;
                MethodInfo? getPreviewMethod = previewInstance
                    .GetType()
                    .GetMethod("GetPreview");

                string previewResult = (string)getPreviewMethod!.Invoke(previewInstance, [options[i]])!;

                optionsBuilder.AppendLine($"{optionNames[i]}: **{previewResult}**");
            }
        }

        embed.WithDescription(optionsBuilder.ToString());

        await ctx.ResponeTryEphemeral(embed, true);
    }

    [Command("option-edit")]
    [Description("Edit the options for the guild.")]
    [Cooldown(15, 3)]
    public static async ValueTask EditOptions(CommandContext ctx, [SlashChoiceProvider<OptionsProvider>] string options, string value) {
        using var scope = ServiceManager.ServiceProvider!.CreateScope();
        SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

        GuildData? guildData = await databaseContent.GuildDataSet
            .Include(g => g.Options)
            .ThenInclude(g => g.ChannelWhitelists)
            .FirstOrDefaultAsync(g => g.GuildId == ctx.Guild!.Id);

        bool guildExist = guildData != null;
        guildData ??= new GuildData() {
            GuildId = ctx.Guild!.Id,
        };

        Type? optionType = typeof(GuildOptions)
            .GetProperties()
            .Where(x => x.GetCustomAttribute<IgnoreAttribute>() == null)
            .First(x => x.Name == options).PropertyType;

        if(optionType == null) {
            await ctx.ResponeTryEphemeral("No option found with that name.", true);
            return;
        }

        MethodInfo? previewMethod = typeof(PreviewManager)
            .GetMethod("GetPreview")
            ?.MakeGenericMethod(optionType);

        var previewInstance = previewMethod?.Invoke(null, null)!;
        if(previewInstance == null) {
            await ctx.ResponeTryEphemeral("An error occurred while trying to edit the options.", true);
            return;
        } else {
            MethodInfo? parseMethod = previewInstance
                .GetType()
                .GetMethod("Parse");
            MethodInfo? getPreviewMethod = previewInstance
                .GetType()
                .GetMethod("GetPreview");

            dynamic? parseResult = parseMethod!.Invoke(previewInstance, [value])!;

            if(!parseResult.Item2) {
                await ctx.ResponeTryEphemeral("Invalid value.", true);
                return;
            }
            string previewResult = (string)getPreviewMethod!.Invoke(previewInstance, [parseResult.Item1])!;

            guildData.Options.GetType().GetProperty(options)!.SetValue(guildData.Options, parseResult.Item1);

            if(!guildExist)
                databaseContent.GuildDataSet.Add(guildData);

            databaseContent.SaveChanges();
            await ctx.ResponeTryEphemeral($"Successfully change `{options}` to **{previewResult}**.", true);
        }
    }
}
