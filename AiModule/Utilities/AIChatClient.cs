using AssistantAI.AiModule.Services.Interfaces;
using OpenAI.Chat;

namespace AssistantAI.AiModule.Utilities;

public class AIChatClient {
    public EventQueue<ChatMessage> ChatMessages;

    public event Action<ChatMessage>? OnMessageAdded {
        add { ChatMessages.OnItemAdded += value; }
        remove { ChatMessages.OnItemAdded -= value; }
    }
    public event Action<ChatMessage>? OnMessageRemoved {
        add { ChatMessages.OnItemRemoved += value; }
        remove { ChatMessages.OnItemRemoved -= value; }
    }

    public readonly IAiResponseService<List<ChatMessage>> AiService;

    public AIChatClient(int maxItems, IAiResponseService<List<ChatMessage>> aiService) {
        AiService = aiService;
        ChatMessages = new EventQueue<ChatMessage>(maxItems);
    }

    public async Task<List<ChatMessage>> PromptAsync(SystemChatMessage systemMessage, List<ChatMessage>? additionalMessages = null) {
        List<ChatMessage> messages = new List<ChatMessage>(additionalMessages ?? []);
        messages.AddRange(ChatMessages.ToList());

        List<ChatMessage> chatMessages = await AiService.PromptAsync(messages, systemMessage);
        foreach(var item in chatMessages) {
            ChatMessages.AddItem(item);
        }

        return chatMessages;
    }

    public async Task<List<ChatMessage>> PromptAsync<Option>(
        SystemChatMessage systemMessage,
        ToolsFunctions<Option> toolsFunctions,
        Option option,
        List<ChatMessage>? additionalMessages = null
    ) where Option : BaseOption {
        if(AiService.GetType().GetInterface(nameof(IAiResponseToolService<List<ChatMessage>>)) == null) {
            throw new InvalidOperationException($"{AiService.GetType().Name} does not implement {nameof(IAiResponseToolService<List<ChatMessage>>)}");
        }

        List<ChatMessage> messages = new List<ChatMessage>(additionalMessages ?? []);
        messages.AddRange(ChatMessages.ToList());

        List<ChatMessage> chatMessages = await ((IAiResponseToolService<List<ChatMessage>>)AiService).PromptAsync(messages, systemMessage, toolsFunctions, option);
        foreach(var item in chatMessages) {
            ChatMessages.AddItem(item);
        }

        return chatMessages;
    }
}