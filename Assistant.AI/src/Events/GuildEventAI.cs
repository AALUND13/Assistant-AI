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

public record ToolTrigger(DiscordGuild? Guild, DiscordChannel? Channel, DiscordUser? User, string? Message);
public record struct ChannelTimerInfo(int Amount, Timer Timer);

// The main class for the AI event.
public partial class GuildEvent : IEventHandler<MessageCreatedEventArgs> {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();

    private readonly IAiResponseToolService<List<ChatMessage>> aiResponseService;
    private readonly IAiResponseService<bool> aiDecisionService;

    private readonly IDatabaseService<Data> databaseService;

    private readonly List<IFilterService> filterServices;

    private readonly DiscordClient client;
    private readonly ToolsFunctions<ToolTrigger> toolsFunctions;

    private readonly Dictionary<ulong, ChannelTimerInfo> channelTypingTimer = [];

    public GuildEvent(IServiceProvider serviceProvider) {
        aiResponseService = serviceProvider.GetRequiredService<IAiResponseToolService<List<ChatMessage>>>();
        aiDecisionService = serviceProvider.GetRequiredService<IAiResponseService<bool>>();
        databaseService = serviceProvider.GetRequiredService<IDatabaseService<Data>>();
        client = serviceProvider.GetRequiredService<DiscordClient>();

        filterServices = serviceProvider.GetServices<IFilterService>().ToList();

        toolsFunctions = new ToolsFunctions<ToolTrigger>(new ToolsFunctionsBuilder<ToolTrigger>()
            .WithToolFunction(GetUserInfo)
            .WithToolFunction(AddOrOverwriteGuildMemory)
            .WithToolFunction(AddOrOverwriteUserMemory)
            .WithToolFunction(RemoveGuildMemory)
            .WithToolFunction(RemoveUserMemory)
        );
        List<string> availableTools = toolsFunctions.ChatTools.Select(tool => tool.FunctionName).ToList();

        LoadMessagesFromDatabase();
    }

    public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs) {
        if(eventArgs.Author.IsBot // Check if the author is a bot
            || eventArgs.Channel.IsPrivate // Check if the channel is a direct message
            || eventArgs.Channel.IsNSFW // Check if the channel is NSFW
            || !eventArgs.Channel.PermissionsFor(eventArgs.Guild.CurrentMember).HasPermission(DiscordPermissions.SendMessages) // Check if the bot has permission to send messages
            || databaseService.Data.GetOrDefaultGuild(eventArgs.Guild.Id).GetOrDefaultGuildUser(eventArgs.Author.Id).ResponsePermission != AIResponsePermission.None // Check if the user is blacklisted
            || databaseService.Data.GetOrDefaultUser(eventArgs.Author.Id).ResponsePermission != AIResponsePermission.None // Check if the user is blacklisted (This is a global blacklist)
            || eventArgs.Message.Content.StartsWith("a!")) // Check if the message is a prefix command
            return;

        ChatMessages.TryAdd(eventArgs.Channel.Id, []);

        var userChatMessage = ChatMessage.CreateUserMessage(HandleDiscordMessage(eventArgs.Message));
        HandleChatMessage(userChatMessage, eventArgs.Channel.Id);

        SystemChatMessage userMemory = GenerateUserMemorySystemMessage(eventArgs.Author.Id);
        SystemChatMessage guildMemory = GenerateGuildMemorySystemMessage(eventArgs.Guild.Id);

        List<ChatMessage> messages = ChatMessages[eventArgs.Channel.Id];
        messages.Insert(0, userMemory);
        messages.Insert(0, guildMemory);

        bool shouldReply = await aiDecisionService.PromptAsync(messages, ChatMessage.CreateSystemMessage(GenerateReplyDecisionPrompt(eventArgs.Message)));
        if(shouldReply) {
            await AddTypingTimerForChannel(eventArgs.Channel);

            ToolTrigger toolTrigger = new ToolTrigger(eventArgs.Guild, eventArgs.Channel, eventArgs.Author, eventArgs.Message.Content);

            List<ChatMessage> assistantChatMessages = await aiResponseService.PromptAsync(messages, ChatMessage.CreateSystemMessage(GenerateSystemPrompt(eventArgs.Message)), toolsFunctions, toolTrigger);
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
            try {
                await channel.TriggerTypingAsync();
            } catch {
                logger.Warn("Failed to trigger typing in channel {0}", channel.Id);
            }
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

    private string GenerateSystemPrompt(DiscordMessage message) {
        return $"""
                You are a Discord bot named {client.CurrentUser.Username}, with the ID {client.CurrentUser.Id}.
                To mention users, use the format <@USERID>.

                Guild Information: [Guild: {message.Channel?.Guild.Name ?? "Unknow"} | ID: {message.Channel?.Guild.Id.ToString() ?? "Unknow"}]
                Channel Information: [Channel: {message.Channel?.Name ?? "Unknow"} | ID: {message.Channel?.Id.ToString() ?? "Unknow"}]

                - Think through each task step by step.
                - Respond with short, clear, and concise replies.
                - Do not include your name or ID in any of your responses.
                - If the user mentions you, you should respond with "How can I assist you today?".
                """;
    }

    private string GenerateReplyDecisionPrompt(DiscordMessage message) {
        return $"""
                You are a Discord bot named {client.CurrentUser.Username}, with the ID {client.CurrentUser.Id}.
                Your decision determines if you should respond to the user. 

                Guild Information: [Guild: {message.Channel?.Guild.Name ?? "Unknow"} | ID: {message.Channel?.Guild.Id.ToString() ?? "Unknow"}]
                Channel Information: [Channel: {message.Channel?.Name ?? "Unknow"} | ID: {message.Channel?.Id.ToString() ?? "Unknow"}]

                Use these guidelines to make your decision:

                - Only respond to messages where you are directly mentioned or tagged.
                - If the message is a question, you may respond.
                - If you are unsure of the answer, do not respond.
                - If the user only mentions you without a question, your decision should be TRUE.
                - You can use the tools available to you [{string.Join(", ", toolsFunctions.ChatTools.Select(tool => tool.FunctionName))}] but only if you decide to respond.
        
                Based on the guidelines above, your decision should be TRUE if you will respond to this message, otherwise it should be FALSE.
                """;
    }
}
