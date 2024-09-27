using AssistantAI.Services.Interfaces;
using NLog;
using OpenAI.Moderations;

namespace AssistantAI.Services {
    public class AIFilterService : IFilterService {
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly ModerationClient moderationClient;

        public AIFilterService(IConfigService config) {
            moderationClient = new ModerationClient("omni-moderation-latest", config.Config.OPENAI_KEY);
        }

        public async Task<string> FilterAsync(string message) {
            bool flagged = (await moderationClient.ClassifyTextAsync(message)).Value.Flagged;

            if(flagged) {
                logger.Info("Flagged message: {Message}", message);
                return "**[Filter]**";
            } else {
                return message;
            }
        }
    }
}
