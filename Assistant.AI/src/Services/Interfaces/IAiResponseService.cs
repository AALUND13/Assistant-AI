using AssistantAI.Utilities;
using OpenAI.Chat;

namespace AssistantAI.Services.Interfaces;

public interface IAiResponseService<T> {
    Task<T> PromptAsync(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage);
}

public interface IAiResponseToolService<T> {
    Task<T> PromptAsync<Option>(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage, ToolsFunctions<Option> toolsFunctions, Option option);
}
