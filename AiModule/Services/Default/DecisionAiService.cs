using AssistantAI.AiModule.Services.Interfaces;
using AssistantAI.AiModule.Utilities.Extension;
using AssistantAI.AiModule.Utilities.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OpenAI.Chat;
using System.ComponentModel.DataAnnotations;

namespace AssistantAI.AiModule.Services.Default;

public readonly record struct Decision([property: Required] string Explanation, [property: Required] bool IsApproved);

public class DecisionAiService : IAiResponseService<bool> {
    private static string ReasoningJsonSchema => typeof(Decision).GetJsonSchemaFromType(new SchemaOptions() { AddDefaultDescription = false }).ToString();

    private readonly ChatClient chatClient;
    private readonly ILogger<DecisionAiService> logger;


    public DecisionAiService(IOptions<OpenAiConfiguration> config, ILogger<DecisionAiService> logger) {
        if(config.Value.ApiKey == null) {
            throw new ArgumentNullException("OpenAI API key is required");
        }

        chatClient = new ChatClient("gpt-4o-mini", config.Value.ApiKey);
        this.logger = logger;
    }

    public async Task<bool> PromptAsync(List<ChatMessage> messages) {
        var buildMessages = messages;
        var userMessage = messages.Last().GetTextMessagePart().Text;

        var chatCompletionOptions = new ChatCompletionOptions() {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "reasoning",
                jsonSchema: BinaryData.FromString(ReasoningJsonSchema),
                jsonSchemaIsStrict: true
            )
        };


        ChatCompletion chatCompletion;
        try {
            chatCompletion = await chatClient.CompleteChatAsync(buildMessages, chatCompletionOptions);
        } catch(Exception e) {
            logger.LogError(e, "Failed to generate response for message: {UserMessage}", userMessage);
            return false;
        }
        var decision = HandleRespone(chatCompletion);

        logger.LogInformation("Made decision for message: {UserMessage}, with response: {Decision}, explanation: {Explanation}", userMessage, decision.IsApproved, decision.Explanation);

        return decision.IsApproved;
    }

    private Decision HandleRespone(ChatCompletion chatCompletion) {
        switch(chatCompletion.FinishReason) {
            case ChatFinishReason.Stop:
                Decision decision = JsonConvert.DeserializeObject<Decision>(chatCompletion.Content[0].Text);

                return decision;
            default:
                return new Decision("Unable to make a decision", false);
        }
    }
}
