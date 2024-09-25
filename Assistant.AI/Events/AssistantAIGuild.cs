using AssistantAI.Services;
using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities;
using AssistantAI.Utilities.Extension;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.VoiceNext;
using NLog;
using OpenAI.Chat;
using System.ComponentModel;
using System.Text;
using Timer = System.Timers.Timer;

namespace AssistantAI.Events;

public record struct ChannelTimerInfo(int Amount, Timer Timer);

public class AssistantAIGuild : IEventHandler<MessageCreatedEventArgs>, IGuildChatMessages {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();

    private readonly IAiResponseToolService<List<ChatMessage>> aiResponseService;
    private readonly IAiResponseService<bool> aiDecisionService;

    private readonly IDatabaseService<Data> databaseService;

    private readonly DiscordClient client;
    private readonly ToolsFunctions toolsFunctions;

    private readonly string systemPrompt;
    private readonly string replyDecisionPrompt;

    private readonly Dictionary<ulong, ChannelTimerInfo> channelTypingTimer = [];

    public Dictionary<ulong, List<ChatMessage>> ChatMessages { get; init; } = [];
    public Dictionary<ulong, List<ChatMessageData>> SerializedChatMessages => this.SerializeChatMessages();

    public void LoadMessagesFromDatabase() {
        Dictionary<ulong, GuildData> allGuildData = databaseService.Data.GuildData ?? new Dictionary<ulong, GuildData>();
        foreach(ulong guildID in allGuildData.Keys) {
            GuildData guildData = allGuildData[guildID];

            if(guildData.ChatMessages != null)
                ChatMessages.Add(guildID, guildData.ChatMessages.Select(msg => msg.Deserialize()).ToList());
        }
    }

    public void SaveMessagesToDatabase() {
        foreach(ulong guildID in ChatMessages.Keys) {
            GuildData guildData = databaseService.Data.GuildData.GetOrAdd(guildID, new GuildData());
            guildData.ChatMessages = ChatMessages[guildID].Select(msg => msg.Serialize()).ToList();

            databaseService.Data.GuildData[guildID] = guildData;
        }

        databaseService.SaveDatabase();
    }

    public AssistantAIGuild(
        IAiResponseToolService<List<ChatMessage>> aiResponseService,
        IAiResponseService<bool> aiDecisionService,
        IDatabaseService<Data> databaseService,
        DiscordClient client) {
        this.aiResponseService = aiResponseService;
        this.aiDecisionService = aiDecisionService;

        this.databaseService = databaseService;

        this.client = client;
        toolsFunctions = new ToolsFunctions(new ToolsFunctionsBuilder()
            .WithToolFunction(JoinUserVC)
        );

        systemPrompt = $"""
                You are a Discord bot named {client.CurrentUser.Username}, with the ID {client.CurrentUser.Id}.
                To mention users, use the format <@USERID>.

                - Think through each task step by step.
                - Respond with short, clear, and concise replies.
                - Do not include your name or ID in any of your responses.
                - If the user mentions you, you should respond with "How can I assist you today?".
                """;
        replyDecisionPrompt = $"""
                You are a Discord bot named {client.CurrentUser.Username}, with the ID {client.CurrentUser.Id}.
                To mention users, use the format "<@USERID>".

                below is a list of think you SHOULD reply to:
                - If the user asks a question.
                - If the user asks for help.
                - If the user mentions by using the format "<@{client.CurrentUser.Id}>" or "{client.CurrentUser.Username}".
                - If the user replies to your message.

                Anything else you should not reply to.
                """;

        LoadMessagesFromDatabase();
    }

    void JoinUserVC([Description("The user ID to join the voice channel of.")] ulong userID) {
        DiscordChannel? channel = (client.Guilds.Values.SelectMany(guild => guild.VoiceStates.Values)
            .FirstOrDefault(voiceState => voiceState.User.Id == userID)?.Channel)
            ?? throw new ArgumentException($"User with ID {userID} is not in a voice channel.");

        channel.ConnectAsync().Wait();
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

            List<ChatMessage> assistantChatMessages = await aiResponseService.PromptAsync(ChatMessages[eventArgs.Guild.Id], ChatMessage.CreateSystemMessage(systemPrompt), toolsFunctions);
            await eventArgs.Message.RespondAsync(assistantChatMessages.Last().Content[0].Text);

            RemoveTypingTimerForChannel(eventArgs.Channel);
            HandleChatMessage(assistantChatMessages.Last(), eventArgs.Guild.Id);
        }

        SaveMessagesToDatabase();
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
        var channelTimer = new Timer(5000);
        channelTimer.Elapsed += async (sender, e) => {
            await channel.TriggerTypingAsync();
        };

        var channelTimerInfo = channelTypingTimer.GetOrAdd(channel.Id, new ChannelTimerInfo(0, channelTimer));
        channelTimerInfo.Timer.Start();
        channelTimerInfo.Amount++;

        channelTypingTimer.SetOrAdd(channel.Id, channelTimerInfo);

        await channel.TriggerTypingAsync();
    }

    private List<ChatMessageContentPart> HandleDiscordMessage(DiscordMessage discordMessage) {
        List<Uri> imageURL = discordMessage.Attachments
            .Where(attachment => attachment.MediaType!.StartsWith("image"))
            .Select(attachment => new Uri(attachment.Url!)).ToList();

        DiscordMessage? messageReference = discordMessage.ReferencedMessage;
        DiscordUser? referencedUser = messageReference?.Author;
        string? referenceUsername = referencedUser?.GlobalName ?? referencedUser?.Username;

        StringBuilder stringBuilder = new StringBuilder();
        stringBuilder.Append($"User: {discordMessage.Author!.GlobalName}");
        stringBuilder.Append($" | ID: {discordMessage.Author!.Id}");
        if(referenceUsername != null) {
            stringBuilder.Append($" | Replying to: {referenceUsername}");
        }

        var chatMessageContentParts = new List<ChatMessageContentPart> {
            ChatMessageContentPart.CreateTextMessageContentPart($"[{stringBuilder}] {discordMessage.Content}")
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
