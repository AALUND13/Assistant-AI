using AssistantAI.Services.Interfaces;
using Newtonsoft.Json;
using NLog;
using OpenAI.Chat;

namespace AssistantAI.Services;

public readonly record struct Step(string Content);
public readonly record struct Reasoning(Step[] Steps, string Conclusion);

public class ReasoningAiService : IAiResponseService<AssistantChatMessage> {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();
    private readonly static string reasoningJsonSchema = """
        {
            "type": "object",
            "properties": {
                "steps": {
                    "type": "array",
                    "items": {
                        "type": "object",
                        "properties": {
                            "content": { "type": "string" }
                        },
                        "required": ["content"],
                        "additionalProperties": false
                    }
                },
                "conclusion": { "type": "string" }
            },
            "required": ["steps", "conclusion"],
            "additionalProperties": false
        }
        """;

    private readonly IConfigService configService;
    private readonly ChatClient openAIClient;

    public ReasoningAiService(IConfigService configService) {
        this.configService = configService;
        openAIClient = new ChatClient("gpt-4o-mini", this.configService.Config.OpenAIKey);
    }


    public async Task<AssistantChatMessage> PromptAsync(List<ChatMessage> additionalMessages, UserChatMessage chatMessage, SystemChatMessage systemMessage) {
        logger.Debug($"Generating prompt for message: {chatMessage.Content[0].Text}");

        var chatMessages = BuildChatMessages(additionalMessages, systemMessage);

        var chatCompletionOptions = new ChatCompletionOptions() {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                name: "reasoning",
                jsonSchema: BinaryData.FromString(reasoningJsonSchema),
                strictSchemaEnabled: true
            )
        };

        var chatCompletion = await openAIClient.CompleteChatAsync(chatMessages, chatCompletionOptions);
        var assistantChatMessage = HandleRespone(chatCompletion);

        logger.Info($"Generated prompt for message: {chatMessage.Content[0].Text}, with response: {assistantChatMessage.Content[0].Text}");

        return assistantChatMessage;
    }

    private AssistantChatMessage HandleRespone(ChatCompletion chatCompletion) {
        switch(chatCompletion.FinishReason) {
            case ChatFinishReason.Stop:
                Reasoning reasoning = JsonConvert.DeserializeObject<Reasoning>(chatCompletion.ToString());
                return ChatMessage.CreateAssistantMessage(reasoning.Conclusion);
            default:
                return ChatMessage.CreateAssistantMessage("Failed to generate response. Please try again.");
        }
    }

    private List<ChatMessage> BuildChatMessages(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage) {
        var messages = new List<ChatMessage>(additionalMessages);
        messages.Insert(0, systemMessage);
        return messages;
    }
}
