﻿using DSharpPlus.Commands;
using DSharpPlus.Entities;

namespace AssistantAI.DiscordUtilities.EventArgs;

public class HelpFormatterEventArgs : System.EventArgs {
    public DiscordGuild? Guild { get; init; }

    public required DiscordUser User { get; init; }
    public required DiscordChannel Channel { get; init; }
    public required CommandsExtension CommandsExtension { get; init; }

    public int CategoryIndex { get; set; }
}
