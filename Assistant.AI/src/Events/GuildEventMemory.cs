using AssistantAI.AiModule.Services;
using AssistantAI.AiModule.Utilities;
using AssistantAI.DataTypes;
using AssistantAI.Services;
using AssistantAI.Utilities.Extension;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;
using System.Text;

namespace AssistantAI.Events;

// All the data methods/properties of the GuildEvent class.
public partial class GuildEvent {
    public Dictionary<ulong, EventQueue<KeyValuePair<string, string>>> GuildMemory { get; init; } = [];
    public Dictionary<ulong, EventQueue<KeyValuePair<string, string>>> UserMemory { get; init; } = [];

    public SystemChatMessage GenerateUserMemorySystemMessage(ulong userID) {
        EventQueue<KeyValuePair<string, string>> userMemory = UserMemory.GetValueOrDefault(userID, []);

        var stringBuilder = new StringBuilder();
        stringBuilder.Append("These are the user memory keys and values that have been stored\n");
        stringBuilder.Append("You can Add, Remove, or Overwrite a key by using your tools functions\n");
        stringBuilder.Append("User Memory:\n");
        if(userMemory.Count() == 0) {
            stringBuilder.Append("No user memory has been stored\n");
        } else {
            foreach(var (key, memory) in userMemory) {
                stringBuilder.Append($"{key}: {memory}\n");
            }
        }

        return ChatMessage.CreateSystemMessage(stringBuilder.ToString());
    }

    public SystemChatMessage GenerateGuildMemorySystemMessage(ulong guildID) {
        EventQueue<KeyValuePair<string, string>> guildMemory = GuildMemory.GetValueOrDefault(guildID, []);

        var stringBuilder = new StringBuilder();
        stringBuilder.Append("These are the guild memory keys and values that have been stored\n");
        stringBuilder.Append("You can Add, Remove, or Overwrite a key by using your tools functions\n");
        stringBuilder.Append("guild Memory:\n");
        if(guildMemory.Count() == 0) {
            stringBuilder.Append("No guild memory has been stored\n");
        } else {
            foreach(KeyValuePair<string, string> memory in guildMemory) {
                stringBuilder.Append($"{memory.Key}: {memory.Value}\n");
            }
        }

        return ChatMessage.CreateSystemMessage(stringBuilder.ToString());
    }

    private void LoadMessagesFromDatabase() {
        Dictionary<ulong, ChannelData> channels = databaseContent.ChannelDataSet
            .Include(channel => channel.ChatMessages)
            .ThenInclude(msg => msg.ToolCalls)
            .ToDictionary(channel => (ulong)channel.ChannelId, channel => channel);

        Dictionary<ulong, GuildData> guilds = databaseContent.GuildDataSet
            .Include(guild => guild.GuildMemory)
            .ToDictionary(guild => (ulong)guild.GuildId, guild => guild);

        Dictionary<ulong, UserData> users = databaseContent.UserDataSet
            .Include(user => user.UserMemory)
            .ToDictionary(user => (ulong)user.UserId, user => user);

        logger.Info("Loading 'ChatMessages' from the database.");
        foreach(ulong channelID in channels.Keys) {
            ChannelData channelData = channels[channelID];

            if(ChatClientServices.ContainsKey(channelID)) continue;

            bool notExists = ChatClientServices.TryAdd(channelID, new AIChatClientService(serviceProvider));

            foreach(var message in channelData.ChatMessages) {
                var deserializedMessage = message.Deserialize();
                ChatClientServices[channelID].ChatMessages.AddItem(deserializedMessage);
            }

            if(notExists) {
                AddEventForChannel(channelID);
            }
        }

        //TODO: Implement the loading of the memory 'GuildMemory' and 'UserMemory' from the database.

        //logger.Info("Loading 'GuildMemory' from the database.");
        //foreach(ulong guildID in guilds.Keys) {
        //    GuildData guild = guilds[guildID];

        //    foreach(var memory in GuildMemory) {
        //        GuildMemory.Add(guildID, new EventQueue<KeyValuePair<string, string>>(memory.Value.MaxItems));
        //    }
        //}

        //logger.Info("Loading 'UserMemory' from the database.");
        //foreach(ulong userID in users.Keys) {
        //    UserData user = users[userID];

        //    foreach(var memory in UserMemory) {
        //        GuildMemory.Add(userID, new EventQueue<KeyValuePair<string, string>>(memory.Value.MaxItems));
        //    }
        //}

        logger.Info("Successfully loaded all data from the database.");
    }

    //private readonly object saveLock = new();

    //private void SaveMessagesToDatabase() {
    //    lock(saveLock) {
    //        logger.Info("Saving 'ChatMessages' to the database.");
    //        foreach(ulong channelID in ChatMessages.Keys) {
    //            ChannelData? channel = databaseContext.ChannelDataSet
    //                .Include(c => c.ChatMessages)
    //                .FirstOrDefault(c => (ulong)c.ChannelId == channelID);

    //            var serializedChatMessages = ChatMessages[channelID].Select(msg => msg.Serialize()).ToList();

    //            if(channel == null) {
    //                channel = new ChannelData {
    //                    ChannelId = (long)channelID,
    //                    ChatMessages = serializedChatMessages
    //                };
    //                databaseContext.ChannelDataSet.Add(channel);
    //            } else {
    //                channel.ChatMessages = ChatMessages[channelID].Select(msg => msg.Serialize()).ToList();
    //            }
    //        }

    //        logger.Info("Saving 'GuildMemory' to the database.");
    //        foreach(ulong guildID in GuildMemory.Keys) {
    //            GuildData? guild = databaseContext.GuildDataSet
    //                .Include(g => g.GuildMemory)
    //                .FirstOrDefault(g => (ulong)g.GuildId == guildID);

    //            var serializedGuildMemory = GuildMemory[guildID].Select(memory => new GuildMemoryItem() {
    //                Key = memory.Key,
    //                Value = memory.Value,
    //            }).ToList();

    //            if(guild == null) {
    //                guild = new GuildData {
    //                    GuildId = (long)guildID,
    //                    GuildMemory = serializedGuildMemory,
    //                };
    //                databaseContext.GuildDataSet.Add(guild);
    //            } else {
    //                guild.GuildMemory = serializedGuildMemory;
    //            }
    //        }

    //        logger.Info("Saving 'UserMemory' to the database.");
    //        foreach(ulong userID in UserMemory.Keys) {
    //            UserData? user = databaseContext.UserDataSet
    //                .Include(u => u.UserMemory)
    //                .FirstOrDefault(u => (ulong)u.UserId == userID);

    //            var serializedUserMemory = UserMemory[userID].Select(memory => new UserMemoryItem() {
    //                Key = memory.Key,
    //                Value = memory.Value
    //            }).ToList();

    //            if(user == null) {
    //                user = new UserData {
    //                    UserId = (long)userID,
    //                    UserMemory = serializedUserMemory,
    //                };
    //                databaseContext.UserDataSet.Add(user);
    //            } else {
    //                user.UserMemory = serializedUserMemory;
    //            }
    //        }

    //        databaseContext.SaveChanges();
    //        logger.Info("Successfully saved all data to the database.");
    //    }
    //}

    private readonly object saveLock = new();

    // TODO: Implement the saving of the memory 'GuildMemory' and 'UserMemory' to the database.
    private void AddEventForChannel(ulong channelID) {
        Action<ChatMessage> onChatMessageAdded = (ChatMessage chatMessage) => {
            lock(saveLock) {
                using var scope = ServiceManager.ServiceProvider!.CreateScope();
                SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

                var channelData = databaseContent.ChannelDataSet
                    .Include(channel => channel.ChatMessages)
                    .FirstOrDefault(channel => (ulong)channel.ChannelId == channelID);

                if(channelData != null) {
                    channelData.ChatMessages.Add(chatMessage.Serialize());
                    logger.Debug($"Added a ChatMessage to ChannelData: {(chatMessage.Content.Count > 0 ? chatMessage.Content[0].Text : "None")}");
                }

                databaseContent.SaveChanges();
            }
        };

        Action<ChatMessage> onChatMessageRemoved = (ChatMessage chatMessage) => {
            lock(saveLock) {
                using var scope = ServiceManager.ServiceProvider!.CreateScope();
                SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

                var channelData = databaseContent.ChannelDataSet
                    .Include(channel => channel.ChatMessages)
                    .FirstOrDefault(channel => (ulong)channel.ChannelId == channelID);

                if(channelData != null) {
                    channelData.ChatMessages.Remove(chatMessage.Serialize());
                    logger.Debug($"Removed ChatMessage from ChannelData: {(chatMessage.Content.Count > 0 ? chatMessage.Content[0].Text : "None")}");
                }

                databaseContent.SaveChanges();
            }
        };

        // Subscribe the event handler
        ChatClientServices[channelID].OnMessageAdded += onChatMessageAdded;
        ChatClientServices[channelID].OnMessageRemoved += onChatMessageRemoved;
    }
}
