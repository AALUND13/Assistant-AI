using AssistantAI.Services;
using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities.Extension;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;
using NLog;
using OpenAI.Chat;
using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Channels;
using Timer = System.Timers.Timer;

namespace AssistantAI.Events;

public record struct ChannelTimerInfo(int Amount, Timer Timer);

public class AssistantAIGuild : IEventHandler<MessageCreatedEventArgs>, IGuildChatMessages {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();

    private readonly IAiResponseService<List<ChatMessage>> aiResponseService;
    private readonly IAiResponseService<bool> aiDecisionService;

    private readonly DiscordClient client;
    private readonly ChatToolService chatToolService;

    private readonly string systemPrompt;
    private readonly string replyDecisionPrompt;

    private readonly Dictionary<ulong, ChannelTimerInfo> channelTypingTimer = [];

    public Dictionary<ulong, List<ChatMessage>> ChatMessages { get; init; } = [];



    public AssistantAIGuild(
        IAiResponseService<List<ChatMessage>> aiResponseService, 
        IAiResponseService<bool> aiDecisionService, 
        DiscordClient client, 
        ChatToolService chatToolService) 
    {
        this.aiResponseService = aiResponseService;
        this.aiDecisionService = aiDecisionService;

        this.client = client;
        this.chatToolService = chatToolService;

        systemPrompt = $"""
                You are a Discord bot named {client.CurrentUser.Username}, with the ID {client.CurrentUser.Id}.
                To mention users, use the format <@USERID>. You can join a vc by using your function "JoinUserVC".

                - Think through each task step by step.
                - Respond with short, clear, and concise replies.
                - Do not include your name or ID in any of your responses.
                - If the user mentions you, you should respond with "How can I assist you today?".
                """;
        replyDecisionPrompt = $"""
                You are a Discord bot named {client.CurrentUser.Username}, with the ID {client.CurrentUser.Id}.
                To mention users, use the format "<@USERID>".  You can join a vc by using your function "JoinUserVC".

                below is a list of think you SHOULD reply to:
                - If the user asks a question.
                - If the user asks for help.
                - If the user mentions by using the format "<@{client.CurrentUser.Id}>" or "{client.CurrentUser.Username}".
                - If the user replies to your message.

                Anything else you should not reply to.
                """;

        chatToolService.AddToolFunction(JoinUserVC);
        logger.Info(chatToolService.ToolFunctions.Count);
    }

    void JoinUserVC([Description("The user ID to join the voice channel of.")] ulong userID) {
        DiscordChannel? channel = client.Guilds.Values.SelectMany(guild => guild.VoiceStates.Values)
            .FirstOrDefault(voiceState => voiceState.User.Id == userID)?.Channel;

        if(channel == null) {
            logger.Warn("User with ID {UserID} is not in a voice channel.", userID);
            throw new ArgumentException($"User with ID {userID} is not in a voice channel.");
        }

        channel.ConnectAsync().Wait();

        logger.Info("Joined voice channel {ChannelName} in guild {GuildName}.", channel.Name, channel.Guild.Name);
    }

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs) {
        if(eventArgs.Author.IsBot
            || eventArgs.Channel.IsPrivate
            || !eventArgs.Channel.PermissionsFor(eventArgs.Guild.CurrentMember).HasPermission(DiscordPermissions.SendMessages))
            return;

        ChatMessages.TryAdd(eventArgs.Guild.Id, new List<ChatMessage>());

        var userChatMessage = ChatMessage.CreateUserMessage(HandleDiscordMessage(eventArgs.Message));
        HandleChatMessage(userChatMessage, eventArgs.Guild.Id);

        bool shouldReply = await aiDecisionService.PromptAsync(ChatMessages[eventArgs.Guild.Id], ChatMessage.CreateSystemMessage(replyDecisionPrompt));
        if(shouldReply) {
            await AddTypingTimerForChannel(eventArgs.Channel);

            List<ChatMessage> assistantChatMessages = await aiResponseService.PromptAsync(ChatMessages[eventArgs.Guild.Id], ChatMessage.CreateSystemMessage(systemPrompt));
            await eventArgs.Message.RespondAsync(assistantChatMessages.Last().Content[0].Text);

            RemoveTypingTimerForChannel(eventArgs.Channel);
        }
    }

    private void RemoveTypingTimerForChannel(DiscordChannel channel) {
        ChannelTimerInfo channelTimerInfo = channelTypingTimer[channel.Id];
        channelTimerInfo.Amount--;

        if(channelTimerInfo.Amount == 0) {
            channelTimerInfo.Timer.Stop();
            channelTimerInfo.Timer.Dispose();

            channelTypingTimer.Remove(channel.Id);
        }
    }

    private async Task AddTypingTimerForChannel(DiscordChannel channel) {
        var channelTimer = new Timer(1000);
        channelTimer.Elapsed += async (sender, e) => {
            await channel.TriggerTypingAsync();
        };

        var channelTimerInfo = channelTypingTimer.GetOrDefault(channel.Id, new ChannelTimerInfo(0, channelTimer));
        channelTimerInfo.Timer.Start();
        channelTimerInfo.Amount++;

        channelTypingTimer.SetOrAdd(channel.Id, channelTimerInfo);

        await channel.TriggerTypingAsync();
    }

    private List<ChatMessageContentPart> HandleDiscordMessage(DiscordMessage discordMessage) {
        List<Uri> imageURL = discordMessage.Attachments
            .Where(attachment => attachment.MediaType!.StartsWith("image"))
            .Select(attachment => new Uri(attachment.Url!)).ToList();

        var chatMessageContentParts = new List<ChatMessageContentPart> {
            ChatMessageContentPart.CreateTextMessageContentPart($"[User: {discordMessage.Author!.GlobalName} | ID: {discordMessage.Author.Id}] {discordMessage.Content}")
        };

        chatMessageContentParts.AddRange(imageURL.Select(url => ChatMessageContentPart.CreateImageMessageContentPart(url)));
        return chatMessageContentParts;
    }

    public void HandleChatMessage(ChatMessage chatMessage, ulong guildID) {
        ChatMessages[guildID].Add(chatMessage);

        while(ChatMessages[guildID].Count > 50) {
            ChatMessages[guildID].RemoveAt(0);
        }
    }
}
