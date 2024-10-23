using AssistantAI.AiModule.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Moderations;

namespace AssistantAI.AiModule.Services.Default;

public class AIFilterService : IFilterService {
    private readonly ILogger<AIFilterService> logger;
    private readonly ModerationClient moderationClient;

    public AIFilterService(IOptions<OpenAiConfiguration> config, ILogger<AIFilterService> logger) {
        moderationClient = new ModerationClient("omni-moderation-latest", config.Value.ApiKey);
        this.logger = logger;
    }

    public async Task<string?> FilterAsync(string? message) {
        if(string.IsNullOrWhiteSpace(message)) {
            return null;
        }

        bool flagged = (await moderationClient.ClassifyTextAsync(message)).Value.Flagged;

        if(flagged) {
            logger.LogInformation("Flagged message: {Message}", message);
            return "**[Filter]**";
        } else {
            return message;
        }
    }
}
