﻿using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities;
using AssistantAI.Utilities.Extension;
using Newtonsoft.Json;
using NLog;
using OpenAI.Chat;
using System.ComponentModel.DataAnnotations;

namespace AssistantAI.Services;

public readonly record struct Step([property: Required] string Content);
public readonly record struct Reasoning([property: Required] Step[] Steps, [property: Required] string Response);

public class ReasoningAiService : IAiResponseToolService<List<ChatMessage>> {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();
    private static string ReasoningJsonSchema => typeof(Reasoning).GetJsonSchemaFromType(false).ToString();

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
                jsonSchema: BinaryData.FromString(ReasoningJsonSchema),
                jsonSchemaIsStrict: true
            )
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

        Reasoning? reasoning = chatCompletion.Content.Count > 0 ? JsonConvert.DeserializeObject<Reasoning>(chatCompletion.Content[0].Text) : null;
        switch(chatCompletion.FinishReason) {
            case ChatFinishReason.Stop:
                returnMessages.Add(ChatMessage.CreateAssistantMessage(reasoning?.Response));
                break;
            case ChatFinishReason.ToolCalls:
                var assistantChatMessage = new AssistantChatMessage(chatCompletion);
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
