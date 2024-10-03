using AssistantAI.DataTypes;
using OpenAI.Chat;
using System.Reflection;

namespace AssistantAI.Utilities.Extension;

public static class ChatMessageExtensions {
    public static ChatMessageData Serialize(this ChatMessage chatMessage) {
        var role = (ChatMessageRole)(typeof(ChatMessage).GetProperty("Role", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(chatMessage))!;
        var urls = new List<Uri>();

        foreach(ChatMessageContentPart part in chatMessage.Content) {
            if(part.ImageUri != null) {
                urls.Add(part.ImageUri);
            }
        }

        return new ChatMessageData {
            Role = role,
            ContentParts = chatMessage.Content.Select(ctx => new ChatMessageContentPartData(ctx.Text)).ToList(),

            ToolCalls = chatMessage is AssistantChatMessage assistantChatMessage ? assistantChatMessage.ToolCalls
                .Select(toolCall => new ChatToolCallData(toolCall.Id, toolCall.FunctionName, toolCall.FunctionArguments.ToString())).ToList() : null,

            ToolCallId = chatMessage is ToolChatMessage ToolChatMessage ? ToolChatMessage.ToolCallId : null,
        };
    }


    public static ChatMessageContentPart GetTextMessagePart(this ChatMessage chatMessage) {
        return chatMessage.Content.First(ctx => ctx is ChatMessageContentPart part && part.Text != null);
    }

    public static ChatMessage Deserialize(this ChatMessageData chatMessageData) {
        List<ChatMessageContentPart> messageContentParts = [];
        foreach(ChatMessageContentPartData contentPartData in chatMessageData.ContentParts) {
            messageContentParts.Add(ChatMessageContentPart.CreateTextPart(contentPartData.Text));
        }

        List<ChatToolCall>? toolCalls = chatMessageData.ToolCalls?
            .Select(toolCalls => ChatToolCall.CreateFunctionToolCall(toolCalls.Id, toolCalls.FunctionName, BinaryData.FromString(toolCalls.FunctionArguments)))?
            .ToList();

        ChatMessage chatMessage = chatMessageData.Role switch {
            ChatMessageRole.System => ChatMessage.CreateSystemMessage(messageContentParts),
            ChatMessageRole.Assistant => CreateAssistantChatMessage(toolCalls, messageContentParts.Count > 0 ? messageContentParts.Last().Text : null),
            ChatMessageRole.Tool => ChatMessage.CreateToolMessage(chatMessageData.ToolCallId, messageContentParts),
            ChatMessageRole.User => ChatMessage.CreateUserMessage(messageContentParts),

            _ => throw new NotImplementedException(),
        };

        return chatMessage;
    }

    private static AssistantChatMessage CreateAssistantChatMessage(IEnumerable<ChatToolCall>? toolCalls, string? content) {
        if(toolCalls != null && toolCalls.Any()) {
            return ChatMessage.CreateAssistantMessage(toolCalls);
        } else {
            return ChatMessage.CreateAssistantMessage(content);
        }
    }
}
