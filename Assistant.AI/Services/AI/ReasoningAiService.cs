using AssistantAI.Services.Interfaces;
using Newtonsoft.Json;
using NLog;
using OpenAI.Chat;

namespace AssistantAI.Services {
    public readonly record struct Step(string Content);
    public readonly record struct Reasoning(Step[] Steps, string Conclusion);

    public class ReasoningAiService : IAiResponseService<AssistantChatMessage> {
        private readonly static Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly static string _reasoningJsonSchema = """
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

        private readonly IConfigService ConfigService;
        private readonly ChatClient OpenAIClient;

        public ReasoningAiService(IConfigService configService) {
            ConfigService = configService;
            OpenAIClient = new ChatClient("gpt-4o-mini", ConfigService.Config.OpenAIKey);
        }


        public async Task<AssistantChatMessage> PromptAsync(List<ChatMessage> additionalMessages, UserChatMessage chatMessage, SystemChatMessage systemMessage) {
            _logger.Debug($"Generating prompt for message: {chatMessage.Content[0].Text}");

            var chatMessages = BuildChatMessages(additionalMessages, systemMessage);

            var chatCompletionOptions = new ChatCompletionOptions() {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    name: "reasoning",
                    jsonSchema: BinaryData.FromString(_reasoningJsonSchema),
                    strictSchemaEnabled: true
                )
            };

            var chatCompletion = await OpenAIClient.CompleteChatAsync(chatMessages, chatCompletionOptions);
            var assistantChatMessage = HandleRespone(chatCompletion);

            _logger.Info($"Generated prompt for message: {chatMessage.Content[0].Text}, with response: {assistantChatMessage.Content[0].Text}");

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
}
