using AssistantAI.DiscordUtilities.EventArgs;
using AssistantAI.DiscordUtilities;
using AssistantAI.Services;
using AssistantAI;
using DSharpPlus.Commands.Processors.SlashCommands.NamingPolicies;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Entities;
using System.Globalization;
using System.Text;
using DSharpPlus.Commands.Trees;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using DSharpPlus.Commands.ContextChecks;

namespace AssistantAI.DiscordUtilities.HelpFormatters;
public class EmbedHelpFormatter : BaseHelpFormatter {
    public override async Task<IEnumerable<DiscordMessageBuilder>> FormatHelpMessage(Dictionary<string, IEnumerable<Command>> categoryCommandMap, HelpFormatterEventArgs args) {
        IList<DiscordMessageBuilder> formattedMessages = [];
        foreach(var commandCategory in categoryCommandMap) {
            formattedMessages.Add(await FormatSingleHelpMessage(commandCategory.Value, commandCategory.Key, args));
        }

        return formattedMessages;
    }

    public override async Task<Dictionary<string, IEnumerable<Command>>> GetCommandCategories(IEnumerable<Command> allCommands) {
        var categoryCommandMap = new Dictionary<string, List<Command>>();

        foreach(var command in allCommands) {
            if(command.Subcommands.Count == 0) {
                if(!categoryCommandMap.ContainsKey("No Category"))
                    categoryCommandMap["No Category"] = [];

                categoryCommandMap["No Category"].Add(command);
            } else {
                foreach(var subCommand in GetSubCommands(command)) {
                    string category = subCommand.FullName.Split(" ")[0];

                    if(!categoryCommandMap.ContainsKey(category))
                        categoryCommandMap[category] = [];

                    categoryCommandMap[category].Add(subCommand);
                }
            }
        }

        return categoryCommandMap.ToDictionary(k => k.Key, v => v.Value.AsEnumerable());
    }

    private static async Task<DiscordMessageBuilder> FormatSingleHelpMessage(IEnumerable<Command> commands, string categoryName, HelpFormatterEventArgs args) {
        TextInfo currentCultureTextInfo = CultureInfo.CurrentCulture.TextInfo;
        Command rootCommand = GetRootCommand(commands.First());

        DiscordPermissions requireRootCommandPermissions = rootCommand.Attributes
            .OfType<RequirePermissionsAttribute>()
            .FirstOrDefault()?.UserPermissions ?? DiscordPermissions.None;

        var embedBuilder = new DiscordEmbedBuilder()
            .WithTitle($"Commands in '{currentCultureTextInfo.ToTitleCase(categoryName)}'")
            .WithDescription($"""
                The following commands are available in the '**{currentCultureTextInfo.ToTitleCase(categoryName)}**' category.
                {(categoryName != "No Category" ? $"Required permissions: `{DSharpPlus.Utilities.ToPermissionString(requireRootCommandPermissions)}`" : "")}
            """)
            .WithColor(DiscordColor.Azure);

        SlashCommandProcessor? slashCommandProcessor = args.CommandsExtension!.GetProcessor<SlashCommandProcessor>();
        TextCommandProcessor? textCommandProcessor = args.CommandsExtension!.GetProcessor<TextCommandProcessor>();


        foreach(var command in commands) {
            var commandInfoBuilder = new StringBuilder();
            rootCommand = GetRootCommand(command);

            if(slashCommandProcessor != null) {
                var mappedApplicationCommands = slashCommandProcessor.ApplicationCommandMapping;
                ulong applicationCommandId = mappedApplicationCommands.FirstOrDefault(x => x.Value.Name == rootCommand.Name).Key;

                string commandPath = GetCommandPath(command, slashCommandProcessor.Configuration.NamingPolicy);

                commandInfoBuilder.AppendLine($"Slash: </{commandPath}:{applicationCommandId}>");
            }
            if(textCommandProcessor != null) {
                using var scope = ServiceManager.ServiceProvider!.CreateScope();
                SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

                GuildData guildData = await databaseContent.GuildDataSet
                    .Include(g => g.Options)
                    .FirstOrDefaultAsync(g => g.GuildId == args.Guild!.Id)
                    ?? new GuildData();

                commandInfoBuilder.AppendLine($"Prefix: `{guildData.Options.Prefix}{command.FullName}`");
            }

            string commandName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Regex.Replace(command.Name, @"[-_]", " "));
                
            commandInfoBuilder.AppendLine();
            if (categoryName == "No Category") {
                DiscordPermissions requireCommandPermissions = command.Attributes
                    .OfType<RequirePermissionsAttribute>()
                    .FirstOrDefault()?.UserPermissions ?? DiscordPermissions.None;

                commandInfoBuilder.AppendLine($"Permission: `{DSharpPlus.Utilities.ToPermissionString(requireCommandPermissions)}`");
            }
            commandInfoBuilder.AppendLine($"Description: ```\n{command.Description}\n```");

            embedBuilder.AddField(commandName, commandInfoBuilder.ToString());
        }

        return new DiscordMessageBuilder().AddEmbed(embedBuilder);
    }

    private static List<Command> GetSubCommands(Command commandGroup) {
        List<Command> allCommands = [];

        foreach(var command in commandGroup.Subcommands) {
            allCommands.Add(command);

            if(command.Subcommands.Count != 0)
                allCommands.AddRange(GetSubCommands(commandGroup));

        }

        return allCommands;
    }

    private static Command GetRootCommand(Command command) {
        Command parentCommand = command;
        while(parentCommand.Parent != null) {
            parentCommand = parentCommand.Parent;
        }

        return parentCommand;
    }

    private static string GetCommandPath(Command command, IInteractionNamingPolicy namingPolicy) {
        string transformCommandName = namingPolicy.TransformText(command.Name, CultureInfo.InvariantCulture);

        string[] pathSegments = command.FullName.Split(' ');
        pathSegments[^1] = transformCommandName;
        string commandPath = string.Join(" ", pathSegments);

        return commandPath;
    }
}
