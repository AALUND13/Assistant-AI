using OpenAI.Chat;

namespace AssistantAI.Services.Interfaces {
    public interface IGuildChatMessages {
        Dictionary<ulong, List<ChatMessage>> ChatMessages { get; }
    }
}
