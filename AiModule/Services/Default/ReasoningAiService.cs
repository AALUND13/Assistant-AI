using AssistantAI.AiModule.Services.Interfaces;
using AssistantAI.AiModule.Utilities;
using AssistantAI.AiModule.Utilities.Extension;
using AssistantAI.AiModule.Utilities.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI.Chat;
using System.ComponentModel.DataAnnotations;

namespace AssistantAI.AiModule.Services.Default;

public readonly record struct Step([property: Required] string Content);
public readonly record struct Reasoning([property: Required] Step[] Steps, [property: Required] string Response);

public class ReasoningAiService : IAiResponseToolService<List<ChatMessage>> {
    private static string ReasoningJsonSchema => typeof(Reasoning).GetJsonSchemaFromType(new SchemaOptions() { AddDefaultDescription = false }).ToString();

    private readonly ChatClient chatClient;
    private readonly ILogger<ReasoningAiService> logger;


    public ReasoningAiService(ChatClient chatClient, ILogger<ReasoningAiService> logger) {
        this.chatClient = chatClient;
        this.logger = logger;

    }

    public Task<List<ChatMessage>> PromptAsync(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage) {
        return PromptAsync(additionalMessages, systemMessage, new ToolsFunctions<BaseOption>(new ToolsFunctionsBuilder<BaseOption>()), new BaseOption());
    }

public async Task<List<ChatMessage>> PromptAsync<Option>(
        List<ChatMessage> additionalMessages,
        SystemChatMessage systemMessage,
        ToolsFunctions<Option> toolsFunctions,
        Option option
    ) where Option : BaseOption {
        var buildMessages = BuildChatMessages(additionalMessages, systemMessage);
        string userMessage = additionalMessages.Last().GetTextMessagePart().Text;

        var chatCompletionOptions = new ChatCompletionOptions() {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "reasoning",
                jsonSchema: BinaryData.FromString(ReasoningJsonSchema),
                jsonSchemaIsStrict: true
            )
        };

        foreach(ChatTool tool in toolsFunctions.ChatTools) {
            chatCompletionOptions.Tools.Add(tool);
        }

        ChatCompletion chatCompletion;
        try {
            chatCompletion = await chatClient.CompleteChatAsync(buildMessages, chatCompletionOptions);
        } catch(Exception e) {
            logger.LogError(e, "Failed to generate response for message: {UserMessage}", userMessage);
            return [ChatMessage.CreateAssistantMessage("Failed to generate response. Please try again.")];
        }
        var returnMessages = await HandleRespone(chatCompletion, additionalMessages, systemMessage, toolsFunctions, option);

        logger.LogInformation("Generated prompt for message: {UserMessage}, with response: {AssistantMessage}", userMessage, returnMessages.Last().GetTextMessagePart().Text);

        return returnMessages;
    }

    private async Task<List<ChatMessage>> HandleRespone<Option>(
        ChatCompletion chatCompletion,
        List<ChatMessage> additionalMessages,
        SystemChatMessage systemMessage,
        ToolsFunctions<Option> toolsFunctions,
        Option option
        ) where Option : BaseOption {
        var returnMessages = new List<ChatMessage>();
        var messages = new List<ChatMessage>(additionalMessages);

        Reasoning? reasoning = chatCompletion.Content.Count > 0 ? JsonConvert.DeserializeObject<Reasoning>(chatCompletion.Content[0].Text) : null;
        switch(chatCompletion.FinishReason) {
            case ChatFinishReason.Stop:
                returnMessages.Add(ChatMessage.CreateAssistantMessage(reasoning?.Response));
                break;
            case ChatFinishReason.ToolCalls:
                if(option.ToolCallsRecursionCount >= option.MaxToolCallsRecursionCount) {
                    returnMessages.Add(ChatMessage.CreateAssistantMessage("Tool recursion limit reached. Please try again."));
                    return returnMessages;
                }

                var assistantChatMessage = new AssistantChatMessage(chatCompletion);
                returnMessages.Add(assistantChatMessage);


                foreach(ChatToolCall toolCall in chatCompletion.ToolCalls) {
                    logger.LogInformation("Calling tool function: {FunctionName}", toolCall.FunctionName);
                    option.ToolCallsRecursionCount++;

                    string result = toolsFunctions.CallToolFunction(toolCall, option)?.ToString() ?? "Command succeeded.";
                    returnMessages.Add(ChatMessage.CreateToolMessage(toolCall.Id, result));
                }

                messages.AddRange(returnMessages);
                var responeMessages = await PromptAsync(messages, systemMessage, toolsFunctions, option);

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
