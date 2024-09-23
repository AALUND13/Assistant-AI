using AssistantAI.Services;
using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities.Extension;
using DSharpPlus.Commands;
using DSharpPlus.Commands.ContextChecks;
using NLog;

namespace AssistantAI.ContextChecks {
    public class CooldownAttribute : ContextCheckAttribute {
        public TimeSpan Cooldown { get; }
        public CooldownAttribute(int seconds) {
            Cooldown = TimeSpan.FromSeconds(seconds);
        }
    }

    public class CooldownCheck : IContextCheck<CooldownAttribute> {
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();

        private const string ErrorMessage = "You are on cooldown. Please wait {0} before using this command again.";
        private readonly static IDatabaseService<Data> DatabaseService = ServiceManager.GetService<IDatabaseService<Data>>();

        public ValueTask<string?> ExecuteCheckAsync(CooldownAttribute attribute, CommandContext context) {
            Data.UserData userData = DatabaseService.Data.Users.GetOrDefault(context.User.Id, new Data.UserData());
            double cooldown = userData.CommandCooldowns.GetOrDefault(context.Command.FullName, 0);
            double timeLeft = cooldown - DateTimeOffset.UtcNow.ToUnixTimeSeconds();


            if(timeLeft > 0) {
                logger.Debug($"'{context.User.GlobalName}' is on cooldown for command '{context.Command.FullName}' for {timeLeft} seconds.");

                return ValueTask.FromResult<string?>(string.Format(ErrorMessage, $"<t:{(uint)cooldown}:R>"));
            } else {
                userData.CommandCooldowns[context.Command.FullName] = DateTimeOffset.UtcNow.Add(attribute.Cooldown).ToUnixTimeSeconds();
                DatabaseService.Data.Users[context.User.Id] = userData;
                DatabaseService.SaveDatabase();

                return ValueTask.FromResult<string?>(null);
            }
        }
    }
}
