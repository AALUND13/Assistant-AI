using AssistantAI.AiModule.Services.Interfaces;
using AssistantAI.AiModule.Utilities;
using AssistantAI.AiModule.Utilities.Extension;
using AssistantAI.AiModule.Utilities.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OpenAI.Chat;
using System.ComponentModel.DataAnnotations;

namespace AssistantAI.AiModule.Services.Default {
    public class NormalResponseService : IAiResponseToolService<List<ChatMessage>> {
        private readonly ChatClient chatClient;
        private readonly ILogger<ReasoningAiService> logger;

        public NormalResponseService(IOptions<OpenAiConfiguration> config, ILogger<ReasoningAiService> logger) {
            if(config.Value.ApiKey == null) {
                throw new ArgumentNullException("OpenAI API key is required");
            }

            chatClient = new ChatClient("gpt-4o-mini", config.Value.ApiKey);
            this.logger = logger;

        }

        public Task<List<ChatMessage>> PromptAsync(List<ChatMessage> messages) {
            return PromptAsync(messages, new ToolsFunctions<BaseOption>(new ToolsFunctionsBuilder<BaseOption>()), new BaseOption());
        }

        public async Task<List<ChatMessage>> PromptAsync<Option>(
            List<ChatMessage> messages,
            ToolsFunctions<Option> toolsFunctions,
            Option option
        ) where Option : BaseOption {
            var buildMessages = messages;
            string userMessage = messages.Last().GetTextMessagePart().Text;

            var chatCompletionOptions = new ChatCompletionOptions();
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
            var returnMessages = await HandleRespone(chatCompletion, messages, toolsFunctions, option);

            logger.LogInformation("Generated prompt for message: {UserMessage}, with response: {AssistantMessage}", userMessage, returnMessages.Last().GetTextMessagePart().Text);

            return returnMessages;
        }

        private async Task<List<ChatMessage>> HandleRespone<Option>(
            ChatCompletion chatCompletion,
            List<ChatMessage> additionalMessages,
            ToolsFunctions<Option> toolsFunctions,
            Option option
            ) where Option : BaseOption {
            var returnMessages = new List<ChatMessage>();
            var messages = new List<ChatMessage>(additionalMessages);

            string? response = chatCompletion.Content.Count > 0 ? chatCompletion.Content[0].Text : null;
            switch(chatCompletion.FinishReason) {
                case ChatFinishReason.Stop:
                    if(response != null && !string.IsNullOrWhiteSpace(response)) {
                        returnMessages.Add(ChatMessage.CreateAssistantMessage(response));
                    }
                    break;
                case ChatFinishReason.ToolCalls:
                    returnMessages.Add(ChatMessage.CreateAssistantMessage(chatCompletion));

                    foreach(ChatToolCall toolCall in chatCompletion.ToolCalls) {
                        logger.LogInformation("Calling tool function: {FunctionName}", toolCall.FunctionName);
                        option.ToolCallsRecursionCount++;

                        string result = (await toolsFunctions.CallToolFunction(toolCall, option))?.ToString() ?? "Command succeeded.";
                        returnMessages.Add(ChatMessage.CreateToolMessage(toolCall.Id, result));
                    }

                    messages.AddRange(returnMessages);
                    List<ChatMessage>? responeMessages;
                    if(option.ToolCallsRecursionCount >= option.MaxToolCallsRecursionCount) {
                        responeMessages = await PromptAsync(messages, new ToolsFunctions<Option>(new ToolsFunctionsBuilder<Option>()), option);
                    } else {
                        responeMessages = await PromptAsync(messages, toolsFunctions, option);
                    }

                    returnMessages.AddRange(responeMessages);
                    break;
            }

            return returnMessages;
        }
    }
}
