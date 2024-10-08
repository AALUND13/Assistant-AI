using AssistantAI.AiModule.Services;
using AssistantAI.AiModule.Utilities;
using AssistantAI.Services;
using AssistantAI.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;
using System.Text;
using System.Threading.Channels;

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

            bool chatMessageNotInitialized = ChatClientServices.TryAdd(channelID, new AIChatClientService(serviceProvider));

            foreach(var message in channelData.ChatMessages) {
                var deserializedMessage = message.Deserialize();
                ChatClientServices[channelID].ChatMessages.AddItem(deserializedMessage);
            }

            if(chatMessageNotInitialized) {
                AddEventForMessages(channelID);
            }
        }

        logger.Info("Loading 'GuildMemory' from the database.");
        foreach(ulong guildID in guilds.Keys) {
            GuildData guild = guilds[guildID];

            bool memoryNotInitialized = GuildMemory.TryAdd(guildID, new EventQueue<KeyValuePair<string, string>>(50));

            foreach(var memory in GuildMemory) {
                GuildMemory.Add(guildID, new EventQueue<KeyValuePair<string, string>>(memory.Value.MaxItems));
            }

            if(memoryNotInitialized) {
                AddEventForGuildMemory(guildID);
            }
        }

        logger.Info("Loading 'UserMemory' from the database.");
        foreach(ulong userID in users.Keys) {
            UserData user = users[userID];

            bool memoryNotInitialized = UserMemory.TryAdd(userID, new EventQueue<KeyValuePair<string, string>>(50));

            foreach(var memory in UserMemory) {
                GuildMemory.Add(userID, new EventQueue<KeyValuePair<string, string>>(memory.Value.MaxItems));
            }

            if(memoryNotInitialized) {
                AddEventForUserMemory(userID);
            }
        }

        logger.Info("Successfully loaded all data from the database.");
    }

    private readonly object saveLock = new();

    private void AddEventForMessages(ulong channelID) {
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

    private void AddEventForGuildMemory(ulong guildID) {
        Action<KeyValuePair<string, string>> onMemoryAdded = (KeyValuePair<string, string> guildMemory) => {
            lock(saveLock) {
                using var scope = ServiceManager.ServiceProvider!.CreateScope();
                SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

                GuildData? guild = databaseContent.GuildDataSet
                    .Include(g => g.GuildMemory)
                    .FirstOrDefault(g => (ulong)g.GuildId == guildID);

                if(guild != null) {
                    GuildMemoryItem guildMemoryItem = new() {
                        Key = guildMemory.Key,
                        Value = guildMemory.Value,
                    };

                    guild.GuildMemory.Add(guildMemoryItem);
                    logger.Debug($"Added a GuildMemory to GuildData: [{guildMemory.Key}: {guildMemory.Value}]");
                }

                databaseContent.SaveChanges();
            }
        };

        Action<KeyValuePair<string, string>> onMemoryRemoved = (KeyValuePair<string, string> guildMemory) => {
            lock(saveLock) {
                using var scope = ServiceManager.ServiceProvider!.CreateScope();
                SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

                GuildData? guild = databaseContent.GuildDataSet
                    .Include(g => g.GuildMemory)
                    .FirstOrDefault(g => (ulong)g.GuildId == guildID);

                if(guild != null) {
                    var guildMemoryItem = guild.GuildMemory.FirstOrDefault(memory => memory.Key == guildMemory.Key && memory.Value == guildMemory.Value);
                    
                    if(guildMemoryItem != null) {
                        guild.GuildMemory.Remove(guildMemoryItem);
                        logger.Debug($"Removed a GuildMemory to GuildData: [{guildMemory.Key}: {guildMemory.Value}]");
                    }
                }

                databaseContent.SaveChanges();
            }
        };
        UserMemory[guildID].OnItemAdded += onMemoryAdded;
        UserMemory[guildID].OnItemRemoved += onMemoryRemoved;
    }

    private void AddEventForUserMemory(ulong userID) {
        Action<KeyValuePair<string, string>> onMemoryAdded = (KeyValuePair<string, string> guildMemory) => {
            lock(saveLock) {
                using var scope = ServiceManager.ServiceProvider!.CreateScope();
                SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

                UserData? user = databaseContent.UserDataSet
                    .Include(g => g.UserMemory)
                    .FirstOrDefault(g => (ulong)g.UserId == userID);

                if(user != null) {
                    UserMemoryItem UserMemoryItem = new() {
                        Key = guildMemory.Key,
                        Value = guildMemory.Value,
                    };

                    user.UserMemory.Add(UserMemoryItem);
                    logger.Debug($"Added a GuildMemory to GuildData: [{guildMemory.Key}: {guildMemory.Value}]");
                }

                databaseContent.SaveChanges();
            }
        };

        Action<KeyValuePair<string, string>> onMemoryRemoved = (KeyValuePair<string, string> guildMemory) => {
            lock(saveLock) {
                using var scope = ServiceManager.ServiceProvider!.CreateScope();
                SqliteDatabaseContext databaseContent = scope.ServiceProvider.GetRequiredService<SqliteDatabaseContext>();

                UserData? user = databaseContent.UserDataSet
                    .Include(g => g.UserMemory)
                    .FirstOrDefault(g => (ulong)g.UserId == userID);

                if(user != null) {
                    var guildMemoryItem = user.UserMemory.FirstOrDefault(memory => memory.Key == guildMemory.Key && memory.Value == guildMemory.Value);

                    if(guildMemoryItem != null) {
                        user.UserMemory.Remove(guildMemoryItem);
                        logger.Debug($"Removed a GuildMemory to GuildData: [{guildMemory.Key}: {guildMemory.Value}]");
                    }
                }

                databaseContent.SaveChanges();
            }
        };

        UserMemory[userID].OnItemAdded += onMemoryAdded;
        UserMemory[userID].OnItemRemoved += onMemoryRemoved;
    }
}
