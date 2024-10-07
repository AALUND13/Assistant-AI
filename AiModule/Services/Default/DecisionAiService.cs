using AssistantAI.AiModule.Services.Interfaces;
using AssistantAI.AiModule.Utilities;
using AssistantAI.AiModule.Utilities.Extension;
using AssistantAI.AiModule.Utilities.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI.Chat;
using System.ComponentModel.DataAnnotations;

namespace AssistantAI.AiModule.Services.Default;

public readonly record struct Decision([property: Required] string Explanation, [property: Required] bool IsApproved);

public class DecisionAiService : IAiResponseService<bool> {
    private static string ReasoningJsonSchema => typeof(Decision).GetJsonSchemaFromType(new SchemaOptions() { AddDefaultDescription = false }).ToString();

    private readonly ChatClient chatClient;
    private readonly ILogger<DecisionAiService> logger;


    public DecisionAiService(ChatClient chatClient, ILogger<DecisionAiService> logger) {
        this.chatClient = chatClient;
        this.logger = logger;
    }

    public async Task<bool> PromptAsync(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage) {
        var buildMessages = BuildChatMessages(additionalMessages, systemMessage);
        var userMessage = additionalMessages.Last().GetTextMessagePart().Text;

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

    private List<ChatMessage> BuildChatMessages(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage) {
        var messages = new List<ChatMessage>(additionalMessages);
        messages.Insert(0, systemMessage);
        return messages;
    }
}
