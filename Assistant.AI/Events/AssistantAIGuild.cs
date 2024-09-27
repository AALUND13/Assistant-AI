using AssistantAI.DataTypes;
using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities;
using AssistantAI.Utilities.Extension;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using OpenAI.Chat;
using System.Text;
using Timer = System.Timers.Timer;

namespace AssistantAI.Events;

public record struct ChannelTimerInfo(int Amount, Timer Timer);

public partial class AssistantAIGuild : IEventHandler<MessageCreatedEventArgs>, IChannelChatMessages {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();

    private readonly IAiResponseToolService<List<ChatMessage>> aiResponseService;
    private readonly IAiResponseService<bool> aiDecisionService;

    private readonly IDatabaseService<Data> databaseService;

    private readonly List<IFilterService> filterServices;

    private readonly DiscordClient client;
    private readonly ToolsFunctions toolsFunctions;

    private readonly string systemPrompt;
    private readonly string replyDecisionPrompt;

    private readonly Dictionary<ulong, ChannelTimerInfo> channelTypingTimer = [];

    public Dictionary<ulong, List<ChatMessage>> ChatMessages { get; init; } = [];
    public Dictionary<ulong, List<ChatMessageData>> SerializedChatMessages => this.SerializeChatMessages();

    public void LoadMessagesFromDatabase() {
        Dictionary<ulong, ChannelData> channelsData = databaseService.Data.ChannelData;
        foreach(ulong channelID in channelsData.Keys) {
            ChannelData channelData = channelsData[channelID];

            ChatMessages.Add(channelID, channelData.ChatMessages.Select(msg => msg.Deserialize()).ToList());
        }
    }

    public void SaveMessagesToDatabase() {
        foreach(ulong channelID in ChatMessages.Keys) {
            ChannelData channelData = databaseService.Data.GetOrDefaultChannelData(channelID);
            channelData.ChatMessages = ChatMessages[channelID].Select(msg => msg.Serialize()).ToList();

            databaseService.Data.ChannelData[channelID] = channelData;
        }

        databaseService.SaveDatabase();
    }

    public AssistantAIGuild(IServiceProvider serviceProvider) {
        aiResponseService = serviceProvider.GetRequiredService<IAiResponseToolService<List<ChatMessage>>>();
        aiDecisionService = serviceProvider.GetRequiredService<IAiResponseService<bool>>();
        databaseService = serviceProvider.GetRequiredService<IDatabaseService<Data>>();
        client = serviceProvider.GetRequiredService<DiscordClient>();

        filterServices = serviceProvider.GetServices<IFilterService>().ToList();

        toolsFunctions = new ToolsFunctions(new ToolsFunctionsBuilder()
            .WithToolFunction(JoinUserVC)
            .WithToolFunction(GetUserInfo)
        );
        List<string> availableTools = toolsFunctions.ChatTools.Select(tool => tool.FunctionName).ToList();

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
            Your decision determines if you should respond to the user. 

            Use these guidelines to make your decision:

            - Only respond to messages where you are directly mentioned or tagged.
            - If the message is a question, you may respond.
            - If you are unsure of the answer, do not respond.
            - If the user only mentions you without a question, your decision should be TRUE.
            - You can use the tools available to you [{string.Join(", ", availableTools)}] but only if you decide to respond.
    
            Based on the guidelines above, your decision should be TRUE if you will respond to this message, otherwise it should be FALSE.
            """;

        LoadMessagesFromDatabase();
    }

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs) {
        if(eventArgs.Author.IsBot // Check if the author is a bot
            || eventArgs.Channel.IsPrivate // Check if the channel is a direct message
            || eventArgs.Channel.IsNSFW // Check if the channel is NSFW
            || !eventArgs.Channel.PermissionsFor(eventArgs.Guild.CurrentMember).HasPermission(DiscordPermissions.SendMessages) // Check if the bot has permission to send messages
            || databaseService.Data.GuildData.GetValueOrDefault(eventArgs.Guild.Id, new()).BlacklistedUsers.Any(blacklistedUser => blacklistedUser.userID == eventArgs.Author.Id) // Check if the author is blacklisted
            || eventArgs.Message.Content.StartsWith("a!")) // Check if the message is a prefix command
            return;

        ChatMessages.TryAdd(eventArgs.Channel.Id, new List<ChatMessage>());

        var userChatMessage = ChatMessage.CreateUserMessage(HandleDiscordMessage(eventArgs.Message));
        HandleChatMessage(userChatMessage, eventArgs.Channel.Id);

        bool shouldReply = await aiDecisionService.PromptAsync(ChatMessages[eventArgs.Channel.Id], ChatMessage.CreateSystemMessage(replyDecisionPrompt));
        if(shouldReply) {
            await AddTypingTimerForChannel(eventArgs.Channel);

            List<ChatMessage> assistantChatMessages = await aiResponseService.PromptAsync(ChatMessages[eventArgs.Channel.Id], ChatMessage.CreateSystemMessage(systemPrompt), toolsFunctions);
            foreach(var message in assistantChatMessages) {
                var textPartIndex = message.Content.ToList().FindIndex(part => part.Text != null);
                if(textPartIndex == -1) continue;

                string textPart = message.Content[textPartIndex].Text!;

                foreach(var filterService in filterServices) {
                    textPart = await filterService.FilterAsync(textPart);
                }

                message.Content[textPartIndex] = ChatMessageContentPart.CreateTextPart(textPart);
            }

            RemoveTypingTimerForChannel(eventArgs.Channel);
            await eventArgs.Message.RespondAsync(assistantChatMessages.Last().GetTextMessagePart().Text);

            assistantChatMessages.ForEach(msg => HandleChatMessage(msg, eventArgs.Channel.Id));
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

        var chatMessageContentParts = new List<ChatMessageContentPart>();

        chatMessageContentParts.AddRange(imageURL.Select(url => ChatMessageContentPart.CreateImagePart(url)));
        chatMessageContentParts.Add(ChatMessageContentPart.CreateTextPart($"[{stringBuilder}] {discordMessage.Content}"));

        return chatMessageContentParts;
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
}
