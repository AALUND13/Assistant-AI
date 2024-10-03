using AssistantAI.DataTypes;
using AssistantAI.Utilities.Extension;
using OpenAI.Chat;
using System.Text;

namespace AssistantAI.Events;

// All the data methods/properties of the GuildEvent class.
public partial class GuildEvent {
    public Dictionary<ulong, List<ChatMessage>> ChatMessages { get; init; } = [];
    public Dictionary<ulong, List<ChatMessageData>> SerializedChatMessages => SerializeChatMessages();

    public Dictionary<ulong, Dictionary<string, string>> GuildMemory { get; init; } = [];
    public Dictionary<ulong, Dictionary<string, string>> UserMemory { get; init; } = [];

    public void HandleChatMessage(ChatMessage chatMessage, ulong channelID) {
        if(!ChatMessages.ContainsKey(channelID)) {
            ChatMessages[channelID] = new List<ChatMessage>();
        }

        ChatMessages[channelID].Add(chatMessage);

        while(ChatMessages[channelID].Count > 50) {
            ChatMessages[channelID].RemoveAt(0);
        }
    }

    public SystemChatMessage GenerateUserMemorySystemMessage(ulong userID) {
        Dictionary<string, string> userMemory = UserMemory.GetValueOrDefault(userID, []);

        var stringBuilder = new StringBuilder();
        stringBuilder.Append("These are the user memory keys and values that have been stored\n");
        stringBuilder.Append("You can Add, Remove, or Overwrite a key by using your tools functions\n");
        stringBuilder.Append("User Memory:\n");
        if(userMemory.Count == 0) {
            stringBuilder.Append("No user memory has been stored\n");
        } else {
            foreach(var (key, memory) in userMemory) {
                stringBuilder.Append($"{key}: {memory}\n");
            }
        }

        return ChatMessage.CreateSystemMessage(stringBuilder.ToString());
    }

    public SystemChatMessage GenerateGuildMemorySystemMessage(ulong guildID) {
        Dictionary<string, string> guildMemory = GuildMemory.GetValueOrDefault(guildID, []);

        var stringBuilder = new StringBuilder();
        stringBuilder.Append("These are the guild memory keys and values that have been stored\n");
        stringBuilder.Append("You can Add, Remove, or Overwrite a key by using your tools functions\n");
        stringBuilder.Append("guild Memory:\n");
        if(guildMemory.Count == 0) {
            stringBuilder.Append("No guild memory has been stored\n");
        } else {
            foreach(KeyValuePair<string, string> memory in guildMemory) {
                stringBuilder.Append($"{memory.Key}: {memory.Value}\n");
            }
        }

        return ChatMessage.CreateSystemMessage(stringBuilder.ToString());
    }


    private void LoadMessagesFromDatabase() {
        // Load the chat messages from the database.
        Dictionary<ulong, ChannelData> channels = databaseService.Data.Channels;
        foreach(ulong channelID in channels.Keys) {
            ChannelData channelData = channels[channelID];

            ChatMessages.Add(channelID, channelData.ChatMessages.Select(msg => msg.Deserialize()).ToList());
        }

        // Load the guild memory from the database.
        Dictionary<ulong, GuildData> guilds = databaseService.Data.Guilds;
        foreach(ulong guildID in guilds.Keys) {
            GuildData guild = guilds[guildID];

            GuildMemory.Add(guildID, guild.GuildMemory);
        }

        // Load the user memory from the database.
        Dictionary<ulong, UserData> users = databaseService.Data.Users;
        foreach(ulong userID in users.Keys) {
            UserData user = users[userID];

            UserMemory.Add(userID, user.UserMemory);
        }
    }

    private void SaveMessagesToDatabase() {
        // Save the chat messages to the database.
        foreach(ulong channelID in ChatMessages.Keys) {
            ChannelData channel = databaseService.Data.GetOrDefaultChannel(channelID);
            channel.ChatMessages = ChatMessages[channelID].Select(msg => msg.Serialize()).ToList();

            databaseService.Data.Channels[channelID] = channel;
        }

        // Save the guild memory to the database.
        foreach(ulong guildID in GuildMemory.Keys) {
            GuildData guild = databaseService.Data.GetOrDefaultGuild(guildID);
            guild.GuildMemory = GuildMemory[guildID];

            databaseService.Data.Guilds[guildID] = guild;
        }

        // Save the user memory to the database.
        foreach(ulong userID in UserMemory.Keys) {
            UserData user = databaseService.Data.GetOrDefaultUser(userID);
            user.UserMemory = UserMemory[userID];

            databaseService.Data.Users[userID] = user;
        }

        databaseService.SaveDatabase();
    }

    private Dictionary<ulong, List<ChatMessageData>> SerializeChatMessages() {
        var chatMessages = new Dictionary<ulong, List<ChatMessageData>>();

        foreach(KeyValuePair<ulong, List<ChatMessage>> chatMessage in ChatMessages) {
            chatMessages.Add(chatMessage.Key, chatMessage.Value.Select(msg => msg.Serialize()).ToList());
        }

        return chatMessages;
    }
}
