using AssistantAI.DataTypes;
using AssistantAI.Utilities.Extension;
using OpenAI.Chat;

namespace AssistantAI.Events;

// All the data methods/properties of the GuildEvent class.
public partial class GuildEvent {
    public Dictionary<ulong, List<ChatMessage>> ChatMessages { get; init; } = [];
    public Dictionary<ulong, List<ChatMessageData>> SerializedChatMessages => this.SerializeChatMessages();

    public void LoadMessagesFromDatabase() {
        // Load the chat messages from the database.
        Dictionary<ulong, ChannelData> channels = databaseService.Data.Channels;
        foreach(ulong channelID in channels.Keys) {
            ChannelData channelData = channels[channelID];

            ChatMessages.Add(channelID, channelData.ChatMessages.Select(msg => msg.Deserialize()).ToList());
        }
    }

    public void SaveMessagesToDatabase() {
        // Save the chat messages to the database.
        foreach(ulong channelID in ChatMessages.Keys) {
            ChannelData channel = databaseService.Data.GetOrDefaultChannel(channelID);
            channel.ChatMessages = ChatMessages[channelID].Select(msg => msg.Serialize()).ToList();

            databaseService.Data.Channels[channelID] = channel;
        }

        databaseService.SaveDatabase();
    }

    public void HandleChatMessage(ChatMessage chatMessage, ulong channelID) {
        if(!ChatMessages.ContainsKey(channelID)) {
            ChatMessages[channelID] = new List<ChatMessage>();
        }

        ChatMessages[channelID].Add(chatMessage);

        while(ChatMessages[channelID].Count > 50) {
            ChatMessages[channelID].RemoveAt(0);
        }
    }

    private Dictionary<ulong, List<ChatMessageData>> SerializeChatMessages() {
        var chatMessages = new Dictionary<ulong, List<ChatMessageData>>();

        foreach(KeyValuePair<ulong, List<ChatMessage>> chatMessage in ChatMessages) {
            chatMessages.Add(chatMessage.Key, chatMessage.Value.Select(msg => msg.Serialize()).ToList());
        }

        return chatMessages;
    }
}
