using AssistantAI.Services.Interfaces;
using Newtonsoft.Json;
using NLog;
using OpenAI.Chat;

namespace AssistantAI.Services.AI {
    public readonly record struct ReplyDecision(string Explanation, bool ShouldReply);

    public class ReplyDecisionService : IAiResponseService<bool> {
        private readonly static Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly static string _reasoningJsonSchema = """
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

        private readonly IConfigService _configService;
        private readonly ChatClient _openAIClient;

        public ReplyDecisionService(IConfigService configService) {
            _configService = configService;
            _openAIClient = new ChatClient("gpt-4o-mini", _configService.Config.OpenAIKey);
        }


        public async Task<bool> PromptAsync(List<ChatMessage> additionalMessages, UserChatMessage chatMessage, SystemChatMessage systemMessage) {
            _logger.Debug($"Generating prompt for message: {chatMessage.Content[0].Text}");

            var chatMessages = BuildChatMessages(additionalMessages, systemMessage);

            var chatCompletionOptions = new ChatCompletionOptions() {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    name: "reasoning",
                    jsonSchema: BinaryData.FromString(_reasoningJsonSchema),
                    strictSchemaEnabled: true
                )
            };

            var chatCompletion = await _openAIClient.CompleteChatAsync(chatMessages, chatCompletionOptions);
            var shouldReply = HandleRespone(chatCompletion);
            _logger.Info($"Prompt generated. Should reply: {shouldReply}");

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
}
