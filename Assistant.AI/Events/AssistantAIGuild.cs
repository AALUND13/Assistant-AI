using AssistantAI.Services.Interfaces;
using AssistantAI.Utilities.Extension;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using OpenAI.Chat;
using Timer = System.Timers.Timer;

namespace AssistantAI.Events {
    public record struct ChannelTimerInfo(int Amount, Timer Timer);
    public class AssistantAIGuild : IEventHandler<MessageCreatedEventArgs> {
        private readonly IAiResponseService<AssistantChatMessage> _aiResponseService;
        private readonly IAiResponseService<bool> _aiDecisionService;

        private readonly string _systemPrompt;
        private readonly string _replyDecisionPrompt;

        private readonly List<ChatMessage> _chatMessages = new();
        private readonly Dictionary<ulong, ChannelTimerInfo> _channelTypingTimer = new();

        public AssistantAIGuild(IAiResponseService<AssistantChatMessage> aiResponseService, IAiResponseService<bool> aiDecisionService, DiscordClient client) {
            _aiResponseService = aiResponseService;
            _aiDecisionService = aiDecisionService;

            _systemPrompt = $"""
                You are a Discord bot named {client.CurrentUser.Username}, with the ID {client.CurrentUser.Id}.
                To mention users, use the format <@USER_ID>.

                - Think through each task step by step.
                - Respond with short, clear, and concise replies.
                - Do not include your name or ID in any of your responses.
                - If the user mentions you, you should respond with "How can I assist you today?".
                """;
            _replyDecisionPrompt = $"""
                You are a Discord bot named {client.CurrentUser.Username}, with the ID {client.CurrentUser.Id}.
                To mention users, use the format "<@USER_ID>".

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

            var userChatMessage = ChatMessage.CreateUserMessage(HandleDiscordMessage(eventArgs.Message));
            HandleChatMessage(userChatMessage);

            bool shouldReply = await _aiDecisionService.PromptAsync(_chatMessages, userChatMessage, ChatMessage.CreateSystemMessage(_replyDecisionPrompt));
            if(shouldReply) {
                await AddTypingTimerForChannel(eventArgs.Channel);

                AssistantChatMessage assistantChatMessage = await _aiResponseService.PromptAsync(_chatMessages, userChatMessage, ChatMessage.CreateSystemMessage(_systemPrompt));
                await eventArgs.Message.RespondAsync(assistantChatMessage.Content[0].Text);

                RemoveTypingTimerForChannel(eventArgs.Channel);
            }
        }

        private void RemoveTypingTimerForChannel(DiscordChannel channel) {
            ChannelTimerInfo channelTimerInfo = _channelTypingTimer[channel.Id];
            channelTimerInfo.Amount--;

            if(channelTimerInfo.Amount == 0) {
                channelTimerInfo.Timer.Stop();
                _channelTypingTimer.Remove(channel.Id);
            }
        }

        private async Task AddTypingTimerForChannel(DiscordChannel channel) {
            var channelTimer = new Timer(1000);
            channelTimer.Elapsed += async (sender, e) => {
                await channel.TriggerTypingAsync();
            };

            var channelTimerInfo = _channelTypingTimer.GetOrDefault(channel.Id, new ChannelTimerInfo(0, channelTimer));
            channelTimerInfo.Timer.Start();
            channelTimerInfo.Amount++;

            _channelTypingTimer.SetOrAdd(channel.Id, channelTimerInfo);

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

        private void HandleChatMessage(ChatMessage chatMessage) {
            _chatMessages.Add(chatMessage);

            if(_chatMessages.Count > 50) {
                _chatMessages.RemoveAt(0);
            }
        }
    }
}
