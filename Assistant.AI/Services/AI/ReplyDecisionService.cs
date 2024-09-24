using AssistantAI.Services.Interfaces;
using Newtonsoft.Json;
using NLog;
using OpenAI.Chat;

namespace AssistantAI.Services.AI;

public readonly record struct ReplyDecision(string Explanation, bool ShouldReply);

public class ReplyDecisionService : IAiResponseService<bool> {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();
    private readonly static string reasoningJsonSchema = """
        {
            "type": "object",
            "properties": {
                "explanation": { "type": "string" },
                "shouldReply": { "type": "boolean" }
            },
            "required": ["shouldReply", "explanation"],
            "additionalProperties": false
        }
        """;

    private readonly ChatClient openAIClient;

    public ReplyDecisionService(IConfigService configService) {
        openAIClient = new ChatClient("gpt-4o-mini", configService.Config.OpenAIKey);
    }


    public async Task<bool> PromptAsync(List<ChatMessage> additionalMessages, UserChatMessage chatMessage, SystemChatMessage systemMessage) {
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
        var shouldReply = HandleRespone(chatCompletion);
        logger.Info($"Prompt generated. Should reply: {shouldReply}");

        return shouldReply;
    }

    private bool HandleRespone(ChatCompletion chatCompletion) {
        switch(chatCompletion.FinishReason) {
            case ChatFinishReason.Stop:
                ReplyDecision reasoning = JsonConvert.DeserializeObject<ReplyDecision>(chatCompletion.ToString());
                return reasoning.ShouldReply;
            default:
                return false;
        }
    }

    private List<ChatMessage> BuildChatMessages(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage) {
        var messages = new List<ChatMessage>(additionalMessages);
        messages.Insert(0, systemMessage);
        return messages;
    }
}
