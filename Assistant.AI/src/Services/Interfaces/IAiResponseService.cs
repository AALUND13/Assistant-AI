using AssistantAI.Utilities;
using OpenAI.Chat;

namespace AssistantAI.Services.Interfaces;

public interface IAiResponseService<T> {
    Task<T> PromptAsync(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage);
}

public class BaseOption {
    public int ToolCallsRecursionCount = 0; // Whis is use to limit the recursion of the tool to prevent infinite loops
    public int MaxToolCallsRecursionCount = 5; // The max recursion count for the tool
}

public interface IAiResponseToolService<T> {
    Task<T> PromptAsync<Option>(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage, ToolsFunctions<Option> toolsFunctions, Option option) where Option : BaseOption;
}
