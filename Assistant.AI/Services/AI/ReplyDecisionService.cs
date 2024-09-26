using AssistantAI.Services.Interfaces;
using Newtonsoft.Json;
using NLog;
using OpenAI.Chat;
using System.Text.Json.Serialization;

namespace AssistantAI.Services.AI;

public readonly record struct Decision(string Explanation, bool IsApproved);

public class ReplyDecisionService : IAiResponseService<bool> {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();
    private readonly static string reasoningJsonSchema = """
        {
            "type": "object",
            "properties": {
                "explanation": { "type": "string" },
                "isApproved": { "type": "boolean" }
            },
            "required": ["isApproved", "explanation"],
            "additionalProperties": false
        }
        """;

    private readonly ChatClient openAIClient;

    public ReplyDecisionService(IConfigService configService) {
        openAIClient = new ChatClient("gpt-4o-mini", configService.Config.OPENAI_KEY);
    }


    public async Task<bool> PromptAsync(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage) {
        var chatMessages = BuildChatMessages(additionalMessages, systemMessage);

        var chatCompletionOptions = new ChatCompletionOptions() {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                name: "reasoning",
                jsonSchema: BinaryData.FromString(reasoningJsonSchema),
                strictSchemaEnabled: true
            )
        };

        var chatCompletion = await openAIClient.CompleteChatAsync(chatMessages, chatCompletionOptions);
        var decision = HandleRespone(chatCompletion);

        return decision;
    }

    private bool HandleRespone(ChatCompletion chatCompletion) {
        switch(chatCompletion.FinishReason) {
            case ChatFinishReason.Stop:
                Decision decision = JsonConvert.DeserializeObject<Decision>(chatCompletion.ToString());
                logger.Info("Made decision for message: {UserMessage}, with response: {Decision}, explanation: {Explanation}", chatCompletion.Content[0].Text, decision.IsApproved, decision.Explanation);
                
                return decision.IsApproved;
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
