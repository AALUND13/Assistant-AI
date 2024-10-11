using AssistantAI.AiModule.Attributes;
using AssistantAI.Commands.Provider;
using AssistantAI.ContextChecks;
using AssistantAI.Services;
using AssistantAI.Utilities;
using AssistantAI.Utilities.Extensions;
using AssistantAI.Utilities.Interfaces;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NLog;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace AssistantAI.Commands;

[Command("options")]
[Description("Options for the guild.")]
public class OptionsCommands {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    [Command("get")]
    [Description("Get the current options for the guild.")]
    [RequireGuild()]
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

    [Command("edit")]
    [Description("Edit the options for the guild.")]
    [RequireGuild()]
    [RequirePermissions(DiscordPermissions.None, DiscordPermissions.ManageGuild)]
    [Cooldown(5)]
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
            logger.Error($"Converter for type '{optionType.Name}' not found.");
            await ctx.ResponeTryEphemeral("An error occurred while trying to edit the options.", true);
            return;
        } else {
            MethodInfo? parseMethod = previewInstance
                .GetType()
                .GetMethod("Parse");
            MethodInfo? getPreviewMethod = previewInstance
                .GetType()
                .GetMethod("GetPreview");

            dynamic?  parseResult = parseMethod!.Invoke(previewInstance, [value])!;

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
