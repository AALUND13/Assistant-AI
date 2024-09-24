using OpenAI.Chat;

namespace AssistantAI.Services.Interfaces;

public interface IAiResponseService<T> {
    Task<T> PromptAsync(List<ChatMessage> additionalMessages, UserChatMessage chatMessage, SystemChatMessage systemMessage);
}
