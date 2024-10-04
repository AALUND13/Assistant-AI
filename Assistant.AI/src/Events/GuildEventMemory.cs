using AssistantAI.DataTypes;
using AssistantAI.Utilities.Extension;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;
using System.Text;

namespace AssistantAI.Events;

// All the data methods/properties of the GuildEvent class.
public partial class GuildEvent {
    public Dictionary<ulong, List<ChatMessage>> ChatMessages { get; init; } = [];
    public Dictionary<ulong, List<ChannelChatMessageData>> SerializedChatMessages => SerializeChatMessages();

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
        var databaseContent = serviceProvider.GetRequiredService<SqliteDatabaseContext>();

        Dictionary<ulong, ChannelData> channels = databaseContent.ChannelDataSet
            .Include(channel => channel.ChatMessages)
            .ToDictionary(channel => (ulong)channel.ChannelId, channel => channel);

        Dictionary<ulong, GuildData> guilds = databaseContent.GuildDataSet
            .Include(guild => guild.GuildMemory)
            .ToDictionary(guild => (ulong)guild.GuildId, guild => guild);

        Dictionary<ulong, UserData> users = databaseContent.UserDataSet
            .Include(user => user.UserMemory)
            .ToDictionary(user => (ulong)user.UserId, user => user);


        foreach(ulong channelID in channels.Keys) {
            ChannelData channelData = channels[channelID];

            ChatMessages.Add(channelID, channelData.ChatMessages.Select(msg => msg.Deserialize()).ToList());
        }

        foreach(ulong guildID in guilds.Keys) {
            GuildData guild = guilds[guildID];

            GuildMemory.Add(guildID, guild.GuildMemory.Select(memory =>
                new KeyValuePair<string, string>(memory.Key, memory.Value)).ToDictionary());
        }

        foreach(ulong userID in users.Keys) {
            UserData user = users[userID];

            UserMemory.Add(userID, user.UserMemory.Select(memory =>
                new KeyValuePair<string, string>(memory.Key, memory.Value)).ToDictionary());
        }
    }

    private void SaveMessagesToDatabase() {
        var databaseContent = serviceProvider.GetRequiredService<SqliteDatabaseContext>();
        
        // Save the chat messages to the database.
        foreach(ulong channelID in ChatMessages.Keys) {
            ChannelData? channel = databaseContent.ChannelDataSet
                .Include(c => c.ChatMessages)
                .FirstOrDefault(c => (ulong)c.ChannelId == channelID);

            if(channel == null) {
                channel = new ChannelData {
                    ChannelId = (long)channelID,
                    ChatMessages = ChatMessages[channelID].Select(msg => msg.Serialize()).ToList()
                };
                databaseContent.ChannelDataSet.Add(channel);
            } else {
                channel.ChatMessages = ChatMessages[channelID].Select(msg => msg.Serialize()).ToList();
                databaseContent.ChannelDataSet.Update(channel);
            }
        }

        // Save the guild memory to the database.
        foreach(ulong guildID in GuildMemory.Keys) {
            GuildData? guild = databaseContent.GuildDataSet
                .Include(g => g.GuildMemory)
                .FirstOrDefault(g => (ulong)g.GuildId == guildID);

            if(guild == null) {
                guild = new GuildData {
                    GuildId = (long)guildID,
                    GuildMemory = GuildMemory[guildID].Select(memory => new GuildMemoryItem() { Key = memory.Key, Value = memory.Value }).ToList()
                };
                databaseContent.GuildDataSet.Add(guild);
            } else {
                guild.GuildMemory = GuildMemory[guildID].Select(memory => new GuildMemoryItem() { Key = memory.Key, Value = memory.Value }).ToList();
                databaseContent.GuildDataSet.Update(guild);
            }
        }

        // Save the user memory to the database.
        foreach(ulong userID in UserMemory.Keys) {
            UserData? user = databaseContent.UserDataSet
                .Include(u => u.UserMemory)
                .FirstOrDefault(u => (ulong)u.UserId == userID);


            if(user == null) {
                user = new UserData {
                    UserId = (long)userID,
                    UserMemory = UserMemory[userID].Select(memory => new UserMemoryItem() { Key = memory.Key, Value = memory.Value }).ToList()
                };
                databaseContent.UserDataSet.Add(user);
            } else {
                user.UserMemory = UserMemory[userID].Select(memory => new UserMemoryItem() { Key = memory.Key, Value = memory.Value }).ToList();
                databaseContent.UserDataSet.Update(user);
            }
        }

        databaseContent.SaveChanges();
    }

    private Dictionary<ulong, List<ChannelChatMessageData>> SerializeChatMessages() {
        var chatMessages = new Dictionary<ulong, List<ChannelChatMessageData>>();

        foreach(KeyValuePair<ulong, List<ChatMessage>> chatMessage in ChatMessages) {
            chatMessages.Add(chatMessage.Key, chatMessage.Value.Select(msg => msg.Serialize()).ToList());
        }

        return chatMessages;
    }
}
