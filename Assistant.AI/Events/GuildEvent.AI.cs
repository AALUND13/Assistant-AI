using AssistantAI.AiModule.Services;
using AssistantAI.AiModule.Services.Interfaces;
using AssistantAI.AiModule.Utilities;
using AssistantAI.AiModule.Utilities.Extensions;
using AssistantAI.Resources;
using AssistantAI.Services;
using AssistantAI.Utilities;
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

    private readonly SqliteDatabaseContext databaseContext;
    private readonly ResourceHandler<Personalitys> personalitys;

    private readonly static ConcurrentDictionary<ulong, AIChatClientService> ChatClientServices = [];

    public GuildEvent(IServiceProvider serviceProvider, SqliteDatabaseContext databaseContext) {
        logger.Info("Initializing GuildEvent...");
        this.serviceProvider = serviceProvider;
        this.databaseContext = databaseContext;

        aiDecisionService = serviceProvider.GetRequiredService<IAiResponseService<bool>>();
        client = serviceProvider.GetRequiredService<DiscordClient>();

        personalitys = serviceProvider.GetRequiredService<ResourceHandler<Personalitys>>();
        personalitys.LoadResource("Resources/Personalitys.xml");

        toolsFunctions = new ToolsFunctions<ToolTrigger>(new ToolsFunctionsBuilder<ToolTrigger>()
            .WithToolFunction(GetUserInfo)
            .WithToolFunction(AddOrOverwriteGuildMemory)
            .WithToolFunction(AddOrOverwriteUserMemory)
            .WithToolFunction(RemoveGuildMemory)
            .WithToolFunction(RemoveUserMemory)
            .WithToolFunction(GetUserMemory)
            .WithToolFunction(SendDmToUser)
            .WithToolFunction(SendDmToUserID)
        );
        List<string> availableTools = toolsFunctions.ChatTools.Select(tool => tool.FunctionName).ToList();

        logger.Info("Available tools: {0}", string.Join(", ", availableTools));

        LoadMessagesFromDatabase();
        logger.Info("GuildEvent initialized successfully.");
    }

    private bool ShouldIgnoreMessage(MessageCreatedEventArgs eventArgs) {
        logger.Debug("Checking if message should be ignored...");

        ulong guildId = eventArgs.Guild != null! ? eventArgs.Guild.Id : 0;
        GuildData? guildData = guildId != 0 ? GetGuildData(guildId) : null;
        UserData? userData = GetUserData(eventArgs.Author.Id);

        if(guildId != 0 && guildData == null) guildData = new GuildData();
        if(userData == null) userData = new UserData();

        bool guildIgnored = false;
        if(guildId != 0) {
            guildIgnored = eventArgs.Author.IsBot
            || !guildData!.Options.AIEnabled
            || (guildData.Options.ChannelWhitelists.Count > 0 && !guildData.Options.ChannelWhitelists.Any(c => c.ChannelId == eventArgs.Channel.Id))
            || !eventArgs.Channel.PermissionsFor(eventArgs.Guild!.CurrentMember).HasPermission(DiscordPermissions.SendMessages)
            || eventArgs.Message.Content.StartsWith(guildData.Options.Prefix, StringComparison.OrdinalIgnoreCase)
            || (guildData.GuildUsers.FirstOrDefault(u => u.GuildUserId == eventArgs.Author.Id)?.ResponsePermission ?? AIResponsePermission.None) != AIResponsePermission.None;
        }

        bool shouldIgnore = eventArgs.Author.IsBot
            || eventArgs.Channel.IsNSFW
            || userData.ResponsePermission != AIResponsePermission.None
            || (guildId != 0 && guildIgnored);

        if(shouldIgnore)
            logger.Info("Message ignored for User ID: {0}, Guild ID: {1}", eventArgs.Author.Id, eventArgs.Guild?.Id.ToString() ?? "N/A");

        return shouldIgnore;
    }

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs) {
        GuildData? guildData = GetGuildData(eventArgs.Guild?.Id ?? 0);
        guildData ??= new GuildData();

        logger.Info("Handling message event from User ID: {0}, Guild ID: {1}, Message: {2}", eventArgs.Author.Id, eventArgs.Guild?.Id.ToString() ?? "N/A", eventArgs.Message.Content);

        if(ShouldIgnoreMessage(eventArgs)) {
            logger.Info("Message ignored. No further processing required.");
            return;
        }

        var userChatMessage = ChatMessage.CreateUserMessage(HandleDiscordMessage(eventArgs.Message));
        if(ChatClientServices.TryAdd(eventArgs.Channel.Id, new AIChatClientService(serviceProvider))) {
            AddEventForMessages(eventArgs.Channel.Id);
        }
        ChatClientServices[eventArgs.Channel.Id].ChatMessages.AddItem(userChatMessage);

        var templateBuilder = new TemplateBuilder()
            .AddValue("BotName", sender.CurrentUser.Username)
            .AddValue("BotID", sender.CurrentUser.Id.ToString())
            .AddValue("OwnerName", sender.CurrentApplication.Owners!.First().GlobalName)
            .AddValue("OwnerID", sender.CurrentApplication.Owners!.First().Id.ToString())
            .AddValue("GuildName", eventArgs.Guild?.Name ?? "N/A")
            .AddValue("GuildID", eventArgs.Guild?.Id.ToString() ?? "N/A")
            .AddValue("ChannelName", eventArgs.Channel.Name)
            .AddValue("ChannelID", eventArgs.Channel.Id.ToString())
            .AddValue("ToolFunctions", string.Join(", ", toolsFunctions.ChatTools.Select(tool => tool.FunctionName)));

        var currestPersonality = personalitys.Resource.PersonalityList.FirstOrDefault(p => p.Name == personalitys.Resource.CurrentPersonality);
        if (currestPersonality == null) throw new Exception("Personality not found.");
        string mainPrompt = templateBuilder.BuildTemplate(currestPersonality.MainPrompt);
        string replyDecisionPrompt = templateBuilder.BuildTemplate(currestPersonality.ReplyDecisionPrompt);

        SystemChatMessage replyDecisionMessage = ChatMessage.CreateSystemMessage(replyDecisionPrompt);
        SystemChatMessage responeMessages = ChatMessage.CreateSystemMessage(mainPrompt);
        SystemChatMessage userMemory = GenerateUserMemorySystemMessage(eventArgs.Author.Id);

        List<ChatMessage> messages = [];
        messages.Add(userMemory);
        if(eventArgs.Guild! != null!) {
            SystemChatMessage guildMemory = GenerateGuildMemorySystemMessage(eventArgs.Guild.Id);
            messages.Add(guildMemory);
        }
        
        bool shouldReply = eventArgs.Channel.IsPrivate
            || guildData.Options.ShouldAlwaysRespond 
            || await aiDecisionService.PromptAsync([replyDecisionMessage, ..messages, ..ChatClientServices[eventArgs.Channel.Id].ChatMessages]);

        if(shouldReply) {
            logger.Info("Bot decided to reply. Initiating response...");
            await AddTypingTimerForChannel(eventArgs.Channel);

            var toolTrigger = new ToolTrigger(eventArgs.Guild, eventArgs.Channel, eventArgs.Author, eventArgs.Message.Content);

            List<ChatMessage> responseMessages = await ChatClientServices[eventArgs.Channel.Id]
                .PromptAsync(toolsFunctions, toolTrigger, [responeMessages, ..messages]);

            RemoveTypingTimerForChannel(eventArgs.Channel);

            if(responseMessages.Count == 0) {
                logger.Warn("No response generated for message: {0}", eventArgs.Message.Content);
                return;
            }

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

    private GuildData? GetGuildData(ulong guildId) {
        return databaseContext.GuildDataSet
            .Include(guild => guild.Options)
            .ThenInclude(guild => guild.ChannelWhitelists)
            .Include(guild => guild.GuildUsers)
            .Include(guild => guild.GuildMemory)
            .FirstOrDefault(guild => guild.GuildId == guildId);
    }

    private UserData? GetUserData(ulong userId) {
        return databaseContext.UserDataSet
            .Include(user => user.UserMemory)
            .FirstOrDefault(user => user.UserId == userId);
    }
}
