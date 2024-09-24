using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities.Extension;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using NLog;
using OpenAI.Chat;
using Timer = System.Timers.Timer;

namespace AssistantAI.Events {
    public record struct ChannelTimerInfo(int Amount, Timer Timer);
    public class AssistantAIGuild : IEventHandler<MessageCreatedEventArgs>, IGuildChatMessages {
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();

        private readonly IAiResponseService<AssistantChatMessage> aiResponseService;
        private readonly IAiResponseService<bool> aiDecisionService;

        private readonly string systemPrompt;
        private readonly string replyDecisionPrompt;

        private readonly Dictionary<ulong, ChannelTimerInfo> channelTypingTimer = [];

        public Dictionary<ulong, List<ChatMessage>> ChatMessages { get; init; } = [];

        public AssistantAIGuild(IAiResponseService<AssistantChatMessage> aiResponseService, IAiResponseService<bool> aiDecisionService, DiscordClient client) {
            this.aiResponseService = aiResponseService;
            this.aiDecisionService = aiDecisionService;

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
        }

        public async Task HandleEventAsync(DiscordClient sender, MessageCreatedEventArgs eventArgs) {
            if(eventArgs.Author.IsBot
                || eventArgs.Channel.IsPrivate
                || !eventArgs.Channel.PermissionsFor(eventArgs.Guild.CurrentMember).HasPermission(DiscordPermissions.SendMessages))
                return;

            ChatMessages.TryAdd(eventArgs.Guild.Id, new List<ChatMessage>());

            var userChatMessage = ChatMessage.CreateUserMessage(HandleDiscordMessage(eventArgs.Message));
            HandleChatMessage(userChatMessage, eventArgs.Guild.Id);

            bool shouldReply = await aiDecisionService.PromptAsync(ChatMessages[eventArgs.Guild.Id], userChatMessage, ChatMessage.CreateSystemMessage(replyDecisionPrompt));
            if(shouldReply) {
                await AddTypingTimerForChannel(eventArgs.Channel);

                AssistantChatMessage assistantChatMessage = await aiResponseService.PromptAsync(ChatMessages[eventArgs.Guild.Id], userChatMessage, ChatMessage.CreateSystemMessage(systemPrompt));
                await eventArgs.Message.RespondAsync(assistantChatMessage.Content[0].Text);

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

            if(ChatMessages[guildID].Count > 50) {
                ChatMessages[guildID].RemoveAt(0);
            }

            logger.Debug($"Chat message '{chatMessage.Content[0].Text}' added to guild '{guildID}'");
        }
    }
}
