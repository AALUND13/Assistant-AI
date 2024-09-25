using AssistantAI.Services.Interfaces;
using Newtonsoft.Json;
using NLog;
using OpenAI.Chat;

namespace AssistantAI.Services;

public readonly record struct Step(string Content);
public readonly record struct Reasoning(Step[] Steps, string Conclusion);

public class ReasoningAiService : IAiResponseService<List<ChatMessage>> {
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

    private readonly ChatToolService chatToolService;

    public ReasoningAiService(IConfigService configService, ChatToolService chatToolService) {
        this.configService = configService;
        this.chatToolService = chatToolService;

        openAIClient = new ChatClient("gpt-4o-mini", this.configService.Config.OpenAIKey);
    }


    public async Task<List<ChatMessage>> PromptAsync(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage) {
        var buildMessages = BuildChatMessages(additionalMessages, systemMessage);

        var chatCompletionOptions = new ChatCompletionOptions() {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                name: "reasoning",
                jsonSchema: BinaryData.FromString(reasoningJsonSchema),
                strictSchemaEnabled: true
            ),
        };

        foreach(ChatTool tool in chatToolService.ChatTools) {
            chatCompletionOptions.Tools.Add(tool);
        }

        logger.Info(chatToolService.ChatTools.Count);

        var chatCompletion = await openAIClient.CompleteChatAsync(buildMessages, chatCompletionOptions);
        var returnMessages = await HandleRespone(chatCompletion, additionalMessages, systemMessage);

        logger.Info("Generated prompt for message: {UserMessage}, with response: {AssistantMessage}", additionalMessages.Last().Content[0].Text, returnMessages.Last().Content[0].Text);

        return returnMessages;
    }

    private async Task<List<ChatMessage>> HandleRespone(ChatCompletion chatCompletion, List<ChatMessage> additionalMessages, SystemChatMessage systemMessage) {
        var returnMessages = new List<ChatMessage>();
        var messages = new List<ChatMessage>(additionalMessages);

        Reasoning reasoning = JsonConvert.DeserializeObject<Reasoning>(chatCompletion.ToString());
        switch(chatCompletion.FinishReason) {
            case ChatFinishReason.Stop:
                returnMessages.Add(ChatMessage.CreateAssistantMessage(reasoning.Conclusion));
                break;
            case ChatFinishReason.ToolCalls:
                returnMessages.Add(new AssistantChatMessage(chatCompletion.ToolCalls, reasoning.Conclusion) { 
                    FunctionCall = chatCompletion.FunctionCall  
                });
                

                foreach(ChatToolCall toolCall in chatCompletion.ToolCalls) {
                    logger.Info("Calling tool function: {FunctionName}", toolCall.FunctionName);
                    string result = chatToolService.CallToolFunction(toolCall) ?? "Command succeeded.";
                    returnMessages.Add(ChatMessage.CreateToolChatMessage(toolCall.Id, result));
                }

                messages.AddRange(returnMessages);
                var responeMessages = await PromptAsync(messages, systemMessage);

                returnMessages.AddRange(responeMessages);

                break;
            default:
                returnMessages.Add(ChatMessage.CreateAssistantMessage("Failed to generate response. Please try again."));
                break;
        }

        return returnMessages;
    }

    private List<ChatMessage> BuildChatMessages(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage) {
        var messages = new List<ChatMessage>(additionalMessages);
        messages.Insert(0, systemMessage);
        return messages;
    }
}
