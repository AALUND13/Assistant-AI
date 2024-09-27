using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities;
using AssistantAI.Utilities.Extension;
using Newtonsoft.Json;
using NLog;
using OpenAI.Chat;

namespace AssistantAI.Services;

public readonly record struct Step(string Content);
public readonly record struct Reasoning(Step[] Steps, string Conclusion);

public class ReasoningAiService : IAiResponseToolService<List<ChatMessage>> {
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

        openAIClient = new ChatClient("gpt-4o-mini", this.configService.Config.OPENAI_KEY);
    }


    public async Task<List<ChatMessage>> PromptAsync(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage) {
        return await PromptAsync(additionalMessages, systemMessage, new ToolsFunctions(new ToolsFunctionsBuilder()));
    }

    public async Task<List<ChatMessage>> PromptAsync(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage, ToolsFunctions toolsFunctions) {
        var buildMessages = BuildChatMessages(additionalMessages, systemMessage);
        string userMessage = additionalMessages.Last().GetTextMessagePart().Text;

        var chatCompletionOptions = new ChatCompletionOptions() {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "reasoning",
                jsonSchema: BinaryData.FromString(reasoningJsonSchema),
                jsonSchemaIsStrict: true
            ),
        };

        foreach(ChatTool tool in toolsFunctions.ChatTools) {
            chatCompletionOptions.Tools.Add(tool);
        }

        ChatCompletion chatCompletion;
        try {
            chatCompletion = await openAIClient.CompleteChatAsync(buildMessages, chatCompletionOptions);
        } catch (Exception e) {
            logger.Error(e, "Failed to generate response for message: {UserMessage}", userMessage);
            return [ChatMessage.CreateAssistantMessage("Failed to generate response. Please try again.")];
        }
        var returnMessages = await HandleRespone(chatCompletion, additionalMessages, systemMessage, toolsFunctions);

        logger.Info("Generated prompt for message: {UserMessage}, with response: {AssistantMessage}", userMessage, returnMessages.Last().GetTextMessagePart().Text);

        return returnMessages;
    }

    private async Task<List<ChatMessage>> HandleRespone(
        ChatCompletion chatCompletion, 
        List<ChatMessage> additionalMessages, 
        SystemChatMessage systemMessage, 
        ToolsFunctions toolsFunctions) 
    {
        var returnMessages = new List<ChatMessage>();
        var messages = new List<ChatMessage>(additionalMessages);

        Reasoning reasoning = JsonConvert.DeserializeObject<Reasoning>(chatCompletion.ToString());
        switch(chatCompletion.FinishReason) {
            case ChatFinishReason.Stop:
                returnMessages.Add(ChatMessage.CreateAssistantMessage(reasoning.Conclusion));
                break;
            case ChatFinishReason.ToolCalls:
                var assistantChatMessage = new AssistantChatMessage(chatCompletion.ToolCalls);
                returnMessages.Add(assistantChatMessage);
                

                foreach(ChatToolCall toolCall in chatCompletion.ToolCalls) {
                    logger.Info("Calling tool function: {FunctionName}", toolCall.FunctionName);
                    string result = toolsFunctions.CallToolFunction(toolCall)?.ToString() ?? "Command succeeded.";
                    returnMessages.Add(ChatMessage.CreateToolMessage(toolCall.Id, result));
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
