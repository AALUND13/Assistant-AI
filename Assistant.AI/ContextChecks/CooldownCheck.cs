using AssistantAI.Utilities.Extensions;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using System;
using System.Collections.Concurrent;

namespace AssistantAI.ContextChecks;

public class CooldownAttribute : ContextCheckAttribute {
    public TimeSpan Cooldown { get; }
    public CooldownType Type { get; }
    public int MaxUses { get; }

    public CooldownAttribute(int seconds, int maxUses = 1, CooldownType type = CooldownType.User) {
        Cooldown = TimeSpan.FromSeconds(seconds);
        Type = type;
        MaxUses = maxUses;
    }
}

public class CooldownCheck : IContextCheck<CooldownAttribute> {
    private const string ErrorMessage = "You are on cooldown. Please wait {0} before using this command again.";

    private readonly static ConcurrentDictionary<string, CooldownBucket> buckets = [];

    public ValueTask<string?> ExecuteCheckAsync(CooldownAttribute attribute, CommandContext context) {

        if(!buckets.TryGetValue(GetKey(attribute, context), out CooldownBucket bucket)) {
            bucket = new CooldownBucket(context.Command.FullName, context.User.Id, attribute.Cooldown, DateTimeOffset.UtcNow.Add(attribute.Cooldown), 0, attribute.MaxUses);
            buckets.AddOrUpdate(GetKey(attribute, context), bucket, (key, old) => bucket);
        }

        string? message = null;
        if(bucket.IsOnCooldown()) {
            message = string.Format(ErrorMessage, $"<t:{bucket.ResetAt.ToUnixTimeSeconds()}:R>");
        }

        return new ValueTask<string?>(message);
    }

    private string GetKey(CooldownAttribute attribute, CommandContext context) {
        string id = "";

        if(attribute.Type.HasFlag(CooldownType.User)) {
            id += context.User.Id;
        }

        if(attribute.Type.HasFlag(CooldownType.Guild)) {
            id += string.IsNullOrEmpty(id.ToString())
                ? (context.Guild?.Id ?? 0)
                : $"-{context.Guild?.Id ?? 0}";
        }

        if(attribute.Type.HasFlag(CooldownType.Channel)) {
            id += string.IsNullOrEmpty(id.ToString())
                ? context.Channel.Id
                : $"-{context.Channel.Id}";
        }


        return $"{id}-{context.Command.FullName}";
    }
}

[Flags]
public enum CooldownType {
    User,
    Guild,
    Channel
}

public sealed class CooldownBucket {
    public string FullCommandName { get; set; }
    public ulong UserId { get; set; }

    public TimeSpan Cooldown { get; set; }
    public DateTimeOffset ResetAt { get; set; }

    public int Uses { get; set; }
    public int MaxUses { get; set; }

    public CooldownBucket(string fullCommandName, ulong userId, TimeSpan cooldown, DateTimeOffset resetAt, int uses, int maxUses) {
        FullCommandName = fullCommandName;
        UserId = userId;
        Cooldown = cooldown;
        ResetAt = resetAt;
        Uses = uses;
        MaxUses = maxUses;
    }

    public bool IsOnCooldown() {
        if(DateTimeOffset.UtcNow > ResetAt) {
            Uses = 0;
            ResetAt = DateTimeOffset.UtcNow + Cooldown;
        }

        if(Uses >= MaxUses) {
            return true;
        } else {
            Uses++;
            return false;
        }
    }
}
