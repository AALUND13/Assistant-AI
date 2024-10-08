using OpenAI.Chat;

namespace AssistantAI.AiModule.Utilities.Extensions {
    public static class ChatMessageExtensions {
        public static ChatMessageContentPart GetTextMessagePart(this ChatMessage chatMessage) {
            return chatMessage.Content.First(ctx => ctx is ChatMessageContentPart part && part.Text != null);
        }
    }
}
