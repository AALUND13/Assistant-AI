using OpenAI.Chat;

namespace AssistantAI.Services.Interfaces;

public interface IChannelChatMessages {
    Dictionary<ulong, List<ChatMessage>> ChatMessages { get; }
    void HandleChatMessage(ChatMessage chatMessage, ulong channelID);
}
