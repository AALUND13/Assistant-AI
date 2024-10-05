using AssistantAI.Attributes;
using AssistantAI.Commands.Provider;
using AssistantAI.ContextChecks;
using AssistantAI.DataTypes;
using AssistantAI.Services;
using AssistantAI.Utilities.Extension;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using DSharpPlus.Commands.Processors.SlashCommands.ArgumentModifiers;
using DSharpPlus.Entities;
using Microsoft.EntityFrameworkCore;
using NLog;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace AssistantAI.Commands;

[Command("options")]
[Description("Options for the guild.")]
public class OptionsCommands {
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();
    private static readonly Dictionary<Type, Func<string, (object, bool)>> converters = new() {
        { typeof(bool), x =>
            {
                bool success = bool.TryParse(x, out bool result);
                return (result, success);
            }
        },
        { typeof(int), x =>
            {
                bool success = int.TryParse(x, out int result);
                return (result, success);
            }
        },
        { typeof(string), x => (x, true) }  // Strings don't need parsing, so always return true
    };


    [Command("get")]
    [Description("Get the current options for the guild.")]
    [RequireGuild()]
    [Cooldown(5)]
    public static ValueTask GetOptions(CommandContext ctx) {
        SqliteDatabaseContext database = ServiceManager.GetService<SqliteDatabaseContext>();

        GuildData? guildData = database.GuildDataSet.FirstOrDefault(g => (ulong)g.GuildId == ctx.Guild!.Id);
        guildData ??= new GuildData();

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
            optionsBuilder.AppendLine($"**{optionNames[i]}**: `{options[i]?.ToString() ?? "Null"}`");
        }

        embed.WithDescription(optionsBuilder.ToString());

        return ctx.ResponeTryEphemeral(embed, true);
    }

    [Command("edit")]
    [Description("Edit the options for the guild.")]
    [RequireGuild()]
    [RequirePermissions(DiscordPermissions.None, DiscordPermissions.ManageGuild)]
    [Cooldown(5)]
    public static ValueTask EditOptions(CommandContext ctx, [SlashChoiceProvider<OptionsProvider>] string options, string value) {
        SqliteDatabaseContext database = ServiceManager.GetService<SqliteDatabaseContext>();

        GuildData? guildData = database.GuildDataSet
            .Include(g => g.Options)
            .FirstOrDefault(g => (ulong)g.GuildId == ctx.Guild!.Id);

        bool guildExist = guildData != null;
        guildData ??= new GuildData() {
            GuildId = (long)ctx.Guild!.Id,
        };

        Type? optionType = typeof(GuildOptions)
            .GetProperties()
            .Where(x => x.GetCustomAttribute<IgnoreAttribute>() == null)
            .First(x => x.Name == options).PropertyType;

        if(optionType == null) {
            return ctx.ResponeTryEphemeral("No option found with that name.", true);
        }

        converters.TryGetValue(optionType, out Func<string, (object, bool)>? converter);
        if(converter == null) {
            logger.Error($"Converter for type '{optionType.Name}' not found.");
            return ctx.ResponeTryEphemeral("An error occurred while trying to edit the options.", true);
        }

        (object returnValue, bool success) = converter.Invoke(value);
        if(!success) {
            return ctx.ResponeTryEphemeral("Invalid value.", true);
        }

        guildData.Options.GetType().GetProperty(options)!.SetValue(guildData.Options, returnValue);

        if(!guildExist)
            database.GuildDataSet.Add(guildData);

        database.SaveChanges();


        return ctx.ResponeTryEphemeral($"Successfully change `{options}` to `{returnValue}`.", true);
    }
}
