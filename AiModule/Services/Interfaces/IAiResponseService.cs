using AssistantAI.AiModule.Utilities;
using OpenAI.Chat;

namespace AssistantAI.AiModule.Services.Interfaces;

public class BaseOption {
    internal int ToolCallsRecursionCount = 0;

    /// <summary>
    /// this is use to limit the recursion of the tool to prevent infinite loops
    /// </summary>
    public int MaxToolCallsRecursionCount = 5;
}


public interface IAiResponseService<T> {
    Task<T> PromptAsync(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage);
}

public interface IAiResponseToolService<T> : IAiResponseService<T> {
    Task<T> PromptAsync<Option>(List<ChatMessage> additionalMessages, SystemChatMessage systemMessage, ToolsFunctions<Option> toolsFunctions, Option option) where Option : BaseOption;
}
