using AssistantAI.AiModule.Services;
using AssistantAI.AiModule.Utilities;
using AssistantAI.Services;
using AssistantAI.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

namespace AssistantAI.Events;

// All the data methods/properties of the GuildEvent class.
public partial class GuildEvent {
    public readonly static ConcurrentDictionary<ulong, EventList<KeyValuePair<string, string>>> GuildMemory = [];
    public readonly static ConcurrentDictionary<ulong, EventList<KeyValuePair<string, string>>> UserMemory = [];

    public SystemChatMessage GenerateUserMemorySystemMessage(ulong userID) {
        EventList<KeyValuePair<string, string>> userMemory = UserMemory.GetValueOrDefault(userID, []);

        var stringBuilder = new StringBuilder();
        stringBuilder.Append("These are the user memory keys and values that have been stored\n");
        stringBuilder.Append("You can Add, Remove, or Overwrite a key by using your tools functions\n");
        stringBuilder.Append("User Memory:\n");
        if(!userMemory.Any()) {
            stringBuilder.Append("No user memory has been stored\n");
        } else {
            foreach(var (key, memory) in userMemory) {
                stringBuilder.Append($"{key}: {memory}\n");
            }
        }

        return ChatMessage.CreateSystemMessage(stringBuilder.ToString());
    }

    public SystemChatMessage GenerateGuildMemorySystemMessage(ulong guildID) {
        EventList<KeyValuePair<string, string>> guildMemory = GuildMemory.GetValueOrDefault(guildID, []);

        var stringBuilder = new StringBuilder();
        stringBuilder.Append("These are the guild memory keys and values that have been stored\n");
        stringBuilder.Append("You can Add, Remove, or Overwrite a key by using your tools functions\n");
        stringBuilder.Append("guild Memory:\n");
        if(!guildMemory.Any()) {
            stringBuilder.Append("No guild memory has been stored\n");
        } else {
            foreach(KeyValuePair<string, string> memory in guildMemory) {
                stringBuilder.Append($"{memory.Key}: {memory.Value}\n");
            }
        }

        return ChatMessage.CreateSystemMessage(stringBuilder.ToString());
    }

    private void LoadMessagesFromDatabase() {
        Dictionary<ulong, ChannelData> channels = databaseContext.ChannelDataSet
            .Include(channel => channel.ChatMessages)
            .ThenInclude(msg => msg.ToolCalls)
            .ToDictionary(channel => channel.ChannelId, channel => channel);

        Dictionary<ulong, GuildData> guilds = databaseContext.GuildDataSet
            .Include(guild => guild.GuildMemory)
            .ToDictionary(guild => guild.GuildId, guild => guild);

        Dictionary<ulong, UserData> users = databaseContext.UserDataSet
            .Include(user => user.UserMemory)
            .ToDictionary(user => user.UserId, user => user);

        logger.Info("Loading 'ChatMessages' from the database.");
        foreach(ulong channelID in channels.Keys) {
            ChannelData channelData = channels[channelID];

            if(ChatClientServices.ContainsKey(channelID)) continue;

            bool chatMessageNotInitialized = ChatClientServices.TryAdd(channelID, new AIChatClientService(serviceProvider));

            if(chatMessageNotInitialized) {
                foreach(var message in channelData.ChatMessages) {
                    var deserializedMessage = message.Deserialize();
                    ChatClientServices[channelID].ChatMessages.AddItem(deserializedMessage);
                }

                AddEventForMessages(channelID);
            }
        }
        

        logger.Info("Loading 'GuildMemory' from the database.");
        foreach(ulong guildID in guilds.Keys) {
            GuildData guild = guilds[guildID];

            bool memoryNotInitialized = GuildMemory.TryAdd(guildID, new EventList<KeyValuePair<string, string>>(50));

            if(memoryNotInitialized) {
                foreach(var memory in guild.GuildMemory) {
                    GuildMemory[guildID].AddItem(new (memory.Key, memory.Value));
                }

                AddEventForGuildMemory(guildID);
            }
        }

        logger.Info("Loading 'UserMemory' from the database.");
        foreach(ulong userID in users.Keys) {
            UserData user = users[userID];

            bool memoryNotInitialized = UserMemory.TryAdd(userID, new EventList<KeyValuePair<string, string>>(50));

            if(memoryNotInitialized) {
                foreach(var memory in user.UserMemory) {
                    UserMemory[userID].AddItem(new(memory.Key, memory.Value));
                }

                AddEventForUserMemory(userID);
            }
        }

        logger.Info("Successfully loaded all data from the database.");
    }

    private readonly object saveLock = new();

    private void AddEventForMessages(ulong channelID) {
        void onChatMessageAdded(ChatMessage chatMessage) {
            lock(saveLock) {
                using var scope = ServiceManager.ServiceProvider!.CreateScope();
                SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

                var channelData = databaseContent.ChannelDataSet
                    .Include(channel => channel.ChatMessages)
                    .FirstOrDefault(channel => channel.ChannelId == channelID);

                if(channelData != null) {
                    channelData.ChatMessages.Add(chatMessage.Serialize());
                    logger.Debug($"Added a ChatMessage to ChannelData: {(chatMessage.Content.Count > 0 ? chatMessage.Content[0].Text : "None")}");
                } else {
                    ChannelData newChannelData = new() {
                        ChannelId = channelID,
                        ChatMessages = [chatMessage.Serialize()]
                    };

                    databaseContent.ChannelDataSet.Add(newChannelData);
                }

                databaseContent.SaveChanges();
            }
        }

        void onChatMessageRemoved(ChatMessage chatMessage) {
            if(ChatClientServices[channelID].ChatMessages.First().Serialize().Role == ChatMessageRole.Tool) {
                ChatClientServices[channelID].ChatMessages.RemoveItem();
            }
        }

        // Subscribe the event handler
        ChatClientServices[channelID].OnMessageAdded += onChatMessageAdded;
        ChatClientServices[channelID].OnMessageRemoved += onChatMessageRemoved;
    }

    private void AddEventForGuildMemory(ulong guildID) {
        void onMemoryAdded(KeyValuePair<string, string> guildMemory) {
            lock(saveLock) {
                using var scope = ServiceManager.ServiceProvider!.CreateScope();
                SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

                GuildData? guild = databaseContent.GuildDataSet
                    .Include(g => g.GuildMemory)
                    .FirstOrDefault(g => g.GuildId == guildID);

                if(guild != null) {
                    GuildMemoryItem guildMemoryItem = new() {
                        Key = guildMemory.Key,
                        Value = guildMemory.Value,
                    };

                    guild.GuildMemory.Add(guildMemoryItem);
                    logger.Debug($"Added a GuildMemory to GuildData: [{guildMemory.Key}: {guildMemory.Value}]");
                } else {
                    GuildData newGuildData = new() {
                        GuildId = guildID,
                        GuildMemory = [new() { Key = guildMemory.Key, Value = guildMemory.Value }]
                    };

                    databaseContent.GuildDataSet.Add(newGuildData);
                }

                databaseContent.SaveChanges();
            }
        }

        void onMemoryRemoved(KeyValuePair<string, string> guildMemory) {
            lock(saveLock) {
                using var scope = ServiceManager.ServiceProvider!.CreateScope();
                SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

                GuildData? guild = databaseContent.GuildDataSet
                    .Include(g => g.GuildMemory)
                    .FirstOrDefault(g => g.GuildId == guildID);

                if(guild != null) {
                    var guildMemoryItem = guild.GuildMemory.FirstOrDefault(memory => memory.Key == guildMemory.Key && memory.Value == guildMemory.Value);

                    if(guildMemoryItem != null) {
                        guild.GuildMemory.Remove(guildMemoryItem);
                        logger.Debug($"Removed a GuildMemory to GuildData: [{guildMemory.Key}: {guildMemory.Value}]");
                    }
                } else {
                    GuildData newGuildData = new() {
                        GuildId = guildID,
                        GuildMemory = [new() { Key = guildMemory.Key, Value = guildMemory.Value }]
                    };

                    databaseContent.GuildDataSet.Add(newGuildData);
                }

                databaseContent.SaveChanges();
            }
        }
        GuildMemory[guildID].OnItemAdded += onMemoryAdded;
        GuildMemory[guildID].OnItemRemoved += onMemoryRemoved;
    }

    private void AddEventForUserMemory(ulong userID) {
        void onMemoryAdded(KeyValuePair<string, string> guildMemory) {
            lock(saveLock) {
                using var scope = ServiceManager.ServiceProvider!.CreateScope();
                SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

                UserData? user = databaseContent.UserDataSet
                    .Include(g => g.UserMemory)
                    .FirstOrDefault(g => g.UserId == userID);

                if(user != null) {
                    UserMemoryItem UserMemoryItem = new() {
                        Key = guildMemory.Key,
                        Value = guildMemory.Value,
                    };

                    user.UserMemory.Add(UserMemoryItem);
                    logger.Debug($"Added a GuildMemory to GuildData: [{guildMemory.Key}: {guildMemory.Value}]");
                } else {
                    UserData newUser = new() {
                        UserId = userID,
                        UserMemory = [new() { Key = guildMemory.Key, Value = guildMemory.Value }]
                    };

                    databaseContent.UserDataSet.Add(newUser);
                }

                databaseContent.SaveChanges();
            }
        }

        void onMemoryRemoved(KeyValuePair<string, string> guildMemory) {
            lock(saveLock) {
                using var scope = ServiceManager.ServiceProvider!.CreateScope();
                SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

                UserData? user = databaseContent.UserDataSet
                    .Include(g => g.UserMemory)
                    .FirstOrDefault(g => g.UserId == userID);

                if(user != null) {
                    var guildMemoryItem = user.UserMemory.FirstOrDefault(memory => memory.Key == guildMemory.Key && memory.Value == guildMemory.Value);

                    if(guildMemoryItem != null) {
                        user.UserMemory.Remove(guildMemoryItem);
                        logger.Debug($"Removed a GuildMemory to GuildData: [{guildMemory.Key}: {guildMemory.Value}]");
                    }
                } else {
                    UserData newUser = new() {
                        UserId = userID,
                        UserMemory = [new() { Key = guildMemory.Key, Value = guildMemory.Value }]
                    };

                    databaseContent.UserDataSet.Add(newUser);
                }

                databaseContent.SaveChanges();
            }
        }

        UserMemory[userID].OnItemAdded += onMemoryAdded;
        UserMemory[userID].OnItemRemoved += onMemoryRemoved;
    }
}
