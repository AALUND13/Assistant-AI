using AssistantAI.DataTypes;
using AssistantAI.Services.Interfaces;
using OpenAI.Chat;

namespace AssistantAI.Utilities.Extension;

internal static class IGuildChatMessagesExtension {
    public static Dictionary<ulong, List<ChatMessageData>> SerializeChatMessages(this IGuildChatMessages guildChatMessages) {
        var chatMessages = new Dictionary<ulong, List<ChatMessageData>>();

        foreach(KeyValuePair<ulong, List<ChatMessage>> chatMessage in guildChatMessages.ChatMessages) {
            chatMessages.Add(chatMessage.Key, chatMessage.Value.Select(msg => msg.Serialize()).ToList());
        }

        return chatMessages;
    }

    public static void DeserializeChatMessages(this IGuildChatMessages guildChatMessages, Dictionary<ulong, List<ChatMessageData>> chatMessages) {
        foreach(KeyValuePair<ulong, List<ChatMessageData>> chatMessage in chatMessages) {
            guildChatMessages.ChatMessages.Add(chatMessage.Key, chatMessage.Value.Select(msg => msg.Deserialize()).ToList());
        }
    }
}
