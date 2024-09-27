using AssistantAI.DataTypes;
using AssistantAI.Services;
using NLog;
using OpenAI.Chat;
using System.Reflection;

namespace AssistantAI.Utilities.Extension;

public static class ChatMessageExtensions {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();
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
            ContentParts = chatMessage.Content.Select(ctx => new ChatMessageContentPartData(ctx.Text, ctx.ImageUri)).ToList(),

            ToolCalls = chatMessage is AssistantChatMessage assistantChatMessage ? assistantChatMessage.ToolCalls
                .Select(toolCall => new ChatToolCallData(toolCall.Id, toolCall.FunctionName, toolCall.FunctionArguments)).ToList() : null,

            ToolCallId = chatMessage is ToolChatMessage ToolChatMessage ? ToolChatMessage.ToolCallId : null,
        };
    }

    public static ChatMessage Deserialize(this ChatMessageData chatMessageData) {
        List<ChatMessageContentPart> messageContentParts = [];
        foreach(ChatMessageContentPartData contentPartData in chatMessageData.ContentParts) {
            if(contentPartData.ImageUri != null) {
                messageContentParts.Add(ChatMessageContentPart.CreateImageMessageContentPart(contentPartData.ImageUri));
            } else {
                messageContentParts.Add(ChatMessageContentPart.CreateTextMessageContentPart(contentPartData.Text));
            }
        }

        List<ChatToolCall>? toolCalls = chatMessageData.ToolCalls?
            .Select(toolCalls => ChatToolCall.CreateFunctionToolCall(toolCalls.Id, toolCalls.FunctionName, toolCalls.FunctionArguments))?
            .ToList();

        ChatMessage chatMessage = chatMessageData.Role switch {
            ChatMessageRole.System => ChatMessage.CreateSystemMessage(messageContentParts),
            ChatMessageRole.Assistant => ChatMessage.CreateAssistantMessage(toolCalls, messageContentParts.Count > 0 ? messageContentParts.Last().Text : null),
            ChatMessageRole.Tool => ChatMessage.CreateToolChatMessage(chatMessageData.ToolCallId, messageContentParts),
            ChatMessageRole.User => ChatMessage.CreateUserMessage(messageContentParts),

            _ => throw new NotImplementedException(),
        };

        return chatMessage;
    }
}
