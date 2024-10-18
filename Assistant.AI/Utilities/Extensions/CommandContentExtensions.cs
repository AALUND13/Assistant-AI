using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Entities;

namespace AssistantAI.Utilities.Extensions;

public static class CommandContentExtensions {
    public static ValueTask ResponeTryEphemeral(this CommandContext ctx, string content, bool isEphemeral = false) {
        if(ctx is SlashCommandContext slashCommandContext)
            return slashCommandContext.RespondAsync(content, isEphemeral);
        else
            return ctx.RespondAsync(content);
    }

    public static ValueTask ResponeTryEphemeral(this CommandContext ctx, DiscordEmbed embed, bool isEphemeral = false) {
        if(ctx is SlashCommandContext slashCommandContext)
            return slashCommandContext.RespondAsync(embed, isEphemeral);
        else
            return ctx.RespondAsync(embed);
    }

    public static ValueTask ResponeTryEphemeral(this CommandContext ctx, DiscordMessageBuilder messageBuilder, bool isEphemeral = false) {
        if(ctx is SlashCommandContext slashCommandContext)
            return slashCommandContext.RespondAsync(new DiscordInteractionResponseBuilder(messageBuilder).AsEphemeral(isEphemeral));
        else
            return ctx.RespondAsync(messageBuilder);
    }
}
