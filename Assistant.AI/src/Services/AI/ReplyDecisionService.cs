using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities.Extension;
using Newtonsoft.Json;
using NLog;
using OpenAI.Chat;
using System.ComponentModel.DataAnnotations;

namespace AssistantAI.Services.AI;

public readonly record struct Decision([property: Required] string Explanation, [property: Required] bool IsApproved);

public class ReplyDecisionService : IAiResponseService<bool> {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();
    private static string ReasoningJsonSchema => typeof(Decision).GetJsonSchemaFromType(false).ToString();

    private readonly ChatClient openAIClient;

    public ReplyDecisionService(IConfigService configService) {
        openAIClient = new ChatClient("gpt-4o-mini", configService.Config.OPENAI_KEY);
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
            chatCompletion = await openAIClient.CompleteChatAsync(buildMessages, chatCompletionOptions);
        } catch(Exception e) {
            logger.Error(e, "Failed to generate response for message: {UserMessage}", userMessage);
            return false;
        }
        var decision = HandleRespone(chatCompletion);

        logger.Info("Made decision for message: {UserMessage}, with response: {Decision}, explanation: {Explanation}", userMessage, decision.IsApproved, decision.Explanation);

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
