using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;

namespace AssistantAI.Utilities.Extension;

public static class CommandContentExtensions {
    public static ValueTask ResponeTryEphemeral(this CommandContext ctx, string content, bool isEphemeral = false) {
        if(ctx is SlashCommandContext slashCommandContext)
            return slashCommandContext.RespondAsync(content, isEphemeral);
        else
            return ctx.RespondAsync(content);
    }
}
