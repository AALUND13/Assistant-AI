using AssistantAI.Utilities;
using OpenAI.Chat;

namespace AssistantAI.Services.Interfaces;

public interface IAiResponseService<T> {
    Task<T> PromptAsync(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage);
}

public interface IAiResponseToolService<T> : IAiResponseService<T> {
    Task<T> PromptAsync(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage, ToolsFunctions toolsFunctions);
}
