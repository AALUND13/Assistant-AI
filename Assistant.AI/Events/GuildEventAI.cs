using AssistantAI.AiModule.Services;
using AssistantAI.AiModule.Services.Interfaces;
using AssistantAI.AiModule.Utilities;
using AssistantAI.AiModule.Utilities.Extensions;
using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities.Extensions;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using OpenAI.Chat;
using System.Collections.Concurrent;
using System.Text;
using Timer = System.Timers.Timer;

namespace AssistantAI.Events;

public class ToolTrigger : BaseOption {
    public DiscordGuild? Guild { get; init; }
    public DiscordChannel? Channel { get; init; }
    public DiscordUser? User { get; init; }
    public string? Message { get; init; }

    public ToolTrigger(DiscordGuild? guild, DiscordChannel? channel, DiscordUser? user, string message) {
        Guild = guild;
        Channel = channel;
        User = user;
        Message = message;
    }
}
public record struct ChannelTimerInfo(int Amount, Timer Timer);

// The main class for the AI event.
public partial class GuildEvent : IEventHandler<MessageCreatedEventArgs> {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();
    private readonly static ConcurrentDictionary<ulong, ChannelTimerInfo> channelTypingTimer = [];

    private readonly IAiResponseService<bool> aiDecisionService;

    private readonly IServiceProvider serviceProvider;

    private readonly DiscordClient client;
    private readonly ToolsFunctions<ToolTrigger> toolsFunctions;

    private readonly SqliteDatabaseContext databaseContent;

    private readonly static ConcurrentDictionary<ulong, AIChatClientService> ChatClientServices = [];

    public GuildEvent(IServiceProvider serviceProvider, SqliteDatabaseContext databaseContext) {
        logger.Info("Initializing GuildEvent...");
        this.serviceProvider = serviceProvider;

        aiDecisionService = serviceProvider.GetRequiredService<IAiResponseService<bool>>();
        client = serviceProvider.GetRequiredService<DiscordClient>();

        this.databaseContent = databaseContext;

        toolsFunctions = new ToolsFunctions<ToolTrigger>(new ToolsFunctionsBuilder<ToolTrigger>()
            .WithToolFunction(GetUserInfo)
            .WithToolFunction(AddOrOverwriteGuildMemory)
            .WithToolFunction(AddOrOverwriteUserMemory)
            .WithToolFunction(RemoveGuildMemory)
            .WithToolFunction(RemoveUserMemory)
            .WithToolFunction(GetUserMemory)
        );
        List<string> availableTools = toolsFunctions.ChatTools.Select(tool => tool.FunctionName).ToList();

        logger.Info("Available tools: {0}", string.Join(", ", availableTools));

        LoadMessagesFromDatabase();
        logger.Info("GuildEvent initialized successfully.");
    }

    private bool ShouldIgnoreMessage(MessageCreatedEventArgs eventArgs) {
        logger.Debug("Checking if message should be ignored...");

        GuildData? guildData = databaseContent.GuildDataSet
            .Include(guild => guild.Options)
            .ThenInclude(guild => guild.ChannelWhitelists)
            .FirstOrDefault(guild => guild.GuildId == eventArgs.Guild.Id);

        if(guildData == null) {
            logger.Warn("No guild data found for Guild ID: {0}, using default.", eventArgs.Guild.Id);
            guildData = new GuildData();
        }

        // Retrieve user data from the database
        UserData? userData = databaseContent.UserDataSet.FirstOrDefault(user => (ulong)user.UserId == eventArgs.Author.Id);
        if(userData == null) {
            logger.Warn("No user data found for User ID: {0}, using default.", eventArgs.Author.Id);
            userData = new UserData();
        }

        bool shouldIgnore = eventArgs.Author.IsBot

            || !guildData.Options.AIEnabled
            || eventArgs.Channel.IsPrivate

            || eventArgs.Channel.IsNSFW
            || (guildData.Options.ChannelWhitelists.Count > 0 && !guildData.Options.ChannelWhitelists.Any(c => c.ChannelId == eventArgs.Channel.Id))

            || !eventArgs.Channel.PermissionsFor(eventArgs.Guild.CurrentMember).HasPermission(DiscordPermissions.SendMessages)
            || eventArgs.Message.Content.StartsWith(guildData.Options.Prefix, StringComparison.OrdinalIgnoreCase)

            || (guildData.GuildUsers.FirstOrDefault(u => u.GuildUserId == eventArgs.Author.Id)?.ResponsePermission ?? AIResponsePermission.None) != AIResponsePermission.None
            || userData.ResponsePermission != AIResponsePermission.None;

        if(shouldIgnore)
            logger.Info("Message ignored for User ID: {0}, Guild ID: {1}", eventArgs.Author.Id, eventArgs.Guild.Id);

        return shouldIgnore;
    }

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs) {
        logger.Info("Handling message event from User ID: {0}, Guild ID: {1}", eventArgs.Author.Id, eventArgs.Guild.Id);

        if(ShouldIgnoreMessage(eventArgs)) {
            logger.Info("Message ignored. No further processing required.");
            return;
        }

        var userChatMessage = ChatMessage.CreateUserMessage(HandleDiscordMessage(eventArgs.Message));
        if(ChatClientServices.TryAdd(eventArgs.Channel.Id, new AIChatClientService(serviceProvider))) {
            AddEventForMessages(eventArgs.Channel.Id);
        }


        ChatClientServices[eventArgs.Channel.Id].ChatMessages.AddItem(userChatMessage);

        SystemChatMessage replyDecisionPrompt = ChatMessage.CreateSystemMessage(GenerateReplyDecisionPrompt(eventArgs.Message));
        SystemChatMessage responePrompt = ChatMessage.CreateSystemMessage(GenerateSystemPrompt(eventArgs.Message));

        SystemChatMessage userMemory = GenerateUserMemorySystemMessage(eventArgs.Author.Id);
        SystemChatMessage guildMemory = GenerateGuildMemorySystemMessage(eventArgs.Guild.Id);

        List<ChatMessage> messages = [userMemory, guildMemory];



        bool shouldReply = await aiDecisionService.PromptAsync([replyDecisionPrompt, ..messages, ..ChatClientServices[eventArgs.Channel.Id].ChatMessages]);

        if(shouldReply) {
            logger.Info("Bot decided to reply. Initiating response...");
            await AddTypingTimerForChannel(eventArgs.Channel);

            var toolTrigger = new ToolTrigger(eventArgs.Guild, eventArgs.Channel, eventArgs.Author, eventArgs.Message.Content);

            List<ChatMessage> responseMessages = await ChatClientServices[eventArgs.Channel.Id]
                .PromptAsync(toolsFunctions, toolTrigger, [responePrompt, ..messages]);

            RemoveTypingTimerForChannel(eventArgs.Channel);

            var messageBuilder = new DiscordMessageBuilder();
            string response = responseMessages.Last().GetTextMessagePart().Text;
            if(response.Length > 2000) {
                messageBuilder.WithContent("Content too long to send. Sending as a file instead.");
                messageBuilder.AddFile("Message.txt", new MemoryStream(Encoding.UTF8.GetBytes(response.ToString())), AddFileOptions.CloseStream);
            } else {
                messageBuilder.WithContent(response);
            }
            await eventArgs.Message.RespondAsync(messageBuilder);

            logger.Info("Bot response sent to channel {0}.", eventArgs.Channel.Id);
        }
    }

    private void RemoveTypingTimerForChannel(DiscordChannel channel) {
        channelTypingTimer.AddOrUpdate(channel.Id, key => new ChannelTimerInfo(0, null), (key, current) => {
            var newAmount = current.Amount - 1;
            if(newAmount <= 0) {
                logger.Info("Typing timer removed for channel {0}.", channel.Id);
                current.Timer?.Stop();
                current.Timer?.Dispose();
                return new ChannelTimerInfo(0, current.Timer);
            }
            return new ChannelTimerInfo(newAmount, current.Timer);
        });

        if(channelTypingTimer[channel.Id].Amount <= 0) {
            channelTypingTimer.TryRemove(channel.Id, out var _);
        }
    }


    private async Task AddTypingTimerForChannel(DiscordChannel channel) {
        var channelTimer = new Timer(5000);
        channelTimer.Elapsed += async (sender, e) => {
            try {
                await channel.TriggerTypingAsync();
            } catch(Exception ex) {
                logger.Warn(ex, "Failed to trigger typing in channel {0}", channel.Id);
            }
        };

        var channelTimerInfo = channelTypingTimer.GetOrAdd(channel.Id, new ChannelTimerInfo(0, channelTimer));
        channelTimerInfo.Timer.Start();
        channelTimerInfo.Amount++;

        channelTypingTimer.SetOrAdd(channel.Id, channelTimerInfo);
        logger.Info("Typing timer added for channel {0}.", channel.Id);

        await channel.TriggerTypingAsync();
    }

    private List<ChatMessageContentPart> HandleDiscordMessage(DiscordMessage discordMessage) {
        logger.Info("Processing Discord message from User ID: {0}", discordMessage.Author.Id);

        List<Uri> imageURL = discordMessage.Attachments
            .Where(attachment => attachment.MediaType!.StartsWith("image"))
            .Select(attachment => new Uri(attachment.Url!)).ToList();

        var messageReference = discordMessage.ReferencedMessage;
        var referencedUser = messageReference?.Author;
        string? referenceUsername = referencedUser?.GlobalName ?? referencedUser?.Username;

        var stringBuilder = new StringBuilder();
        stringBuilder.Append($"User: {discordMessage.Author!.GlobalName}");
        stringBuilder.Append($" | ID: {discordMessage.Author!.Id}");
        if(referenceUsername != null) {
            stringBuilder.Append($" | Replying to: {referenceUsername}");
        }

        var chatMessageContentParts = new List<ChatMessageContentPart>();
        chatMessageContentParts.AddRange(imageURL.Select(url => ChatMessageContentPart.CreateImagePart(url)));
        chatMessageContentParts.Add(ChatMessageContentPart.CreateTextPart($"[{stringBuilder}] {discordMessage.Content}"));

        return chatMessageContentParts;
    }

    private string GenerateSystemPrompt(DiscordMessage message) {
        return $"""
            You are a highly capable Discord bot named {client.CurrentUser.Username}, with the ID {client.CurrentUser.Id}. Your task is to assist users effectively, using concise and precise communication.

            ## Guidelines for Interaction:
            - Mention users using the format: `<@USERID>`.
            - Avoid including your name or ID in your responses unless specifically required by the user.
            - If a user mentions you directly, respond with: "How can I assist you today?".
            - Think through each task step by step and provide a clear, short response.
            - Limit responses to key details; avoid unnecessary elaboration.
            - If a tool function fails twice consecutively, stop attempting that function for the task.
    
            ## Contextual Information:
            - **Guild**: {message.Channel?.Guild.Name ?? "Unknown"} | **Guild ID**: {message.Channel?.Guild.Id.ToString() ?? "Unknown"}
            - **Channel**: {message.Channel?.Name ?? "Unknown"} | **Channel ID**: {message.Channel?.Id.ToString() ?? "Unknown"}
            """;
    }


    private string GenerateReplyDecisionPrompt(DiscordMessage message) {
        return $"""
            You are a decision-making Discord bot named {client.CurrentUser.Username}, with the ID {client.CurrentUser.Id}. Your task is to determine whether you should respond based on specific criteria.

            ## Guidelines for Reply Decision:
            - Respond **only** if you are directly mentioned or tagged.
            - If the message is a question, decide if it’s relevant to your task before responding.
            - If you are unsure about the answer, do not respond.
            - If the user mentions you without asking a question, your decision should be **TRUE**.
            - You can use tools such as [{string.Join(", ", toolsFunctions.ChatTools.Select(tool => tool.FunctionName))}], but only if you decide to respond.

            ## Contextual Information:
            - **Guild**: {message.Channel?.Guild.Name ?? "Unknown"} | **Guild ID**: {message.Channel?.Guild.Id.ToString() ?? "Unknown"}
            - **Channel**: {message.Channel?.Name ?? "Unknown"} | **Channel ID**: {message.Channel?.Id.ToString() ?? "Unknown"}

            Based on the guidelines, your decision should be **TRUE** if you will respond to the message. Otherwise, it should be **FALSE**.
            """;
    }
}
