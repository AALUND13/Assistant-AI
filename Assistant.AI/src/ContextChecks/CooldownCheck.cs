﻿using AssistantAI.DataTypes;
using AssistantAI.Services;
using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities.Extension;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using NLog;

namespace AssistantAI.ContextChecks;

public class CooldownAttribute : ContextCheckAttribute {
    public TimeSpan Cooldown { get; }
    public CooldownAttribute(int seconds) {
        Cooldown = TimeSpan.FromSeconds(seconds);
    }
}

public class CooldownCheck : IContextCheck<CooldownAttribute> {
    private const string ErrorMessage = "You are on cooldown. Please wait {0} before using this command again.";
    private readonly static IDatabaseService<Data> DatabaseService = ServiceManager.GetService<IDatabaseService<Data>>();

    public ValueTask<string?> ExecuteCheckAsync(CooldownAttribute attribute, CommandContext context) {
        UserData userData = DatabaseService.Data.Users.GetOrAdd(context.User.Id, new UserData());
        double cooldown = userData.CommandCooldowns.GetOrAdd(context.Command.FullName, 0);
        double timeLeft = cooldown - DateTimeOffset.UtcNow.ToUnixTimeSeconds();


        if(timeLeft > 0) {
            return ValueTask.FromResult<string?>(string.Format(ErrorMessage, $"<t:{(uint)cooldown}:R>"));
        } else {
            userData.CommandCooldowns[context.Command.FullName] = DateTimeOffset.UtcNow.Add(attribute.Cooldown).ToUnixTimeSeconds();
            DatabaseService.Data.Users[context.User.Id] = userData;
            DatabaseService.SaveDatabase();

            return ValueTask.FromResult<string?>(null);
        }
    }
}