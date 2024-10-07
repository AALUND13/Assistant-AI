using AssistantAI.AiModule.Services.Interfaces;
using AssistantAI.AiModule.Utilities;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

namespace AssistantAI.AiModule.Services;

public class AIChatClientService {
    public EventQueue<ChatMessage> ChatMessages = new(100);

    public event Action<ChatMessage>? OnMessageAdded {
        add { ChatMessages.OnItemAdded += value; }
        remove { ChatMessages.OnItemAdded -= value; }
    }
    public event Action<ChatMessage>? OnMessageRemoved {
        add { ChatMessages.OnItemRemoved += value; }
        remove { ChatMessages.OnItemRemoved -= value; }
    }

    private readonly IAiResponseService<List<ChatMessage>> AiService;
    private readonly IEnumerable<IFilterService> filterServices = [];

    public AIChatClientService(IServiceProvider serviceProvider) {
        AiService = serviceProvider.GetRequiredService<IAiResponseService<List<ChatMessage>>>();
        filterServices = serviceProvider.GetServices<IFilterService>();
    }

    public async Task<List<ChatMessage>> PromptAsync(List<ChatMessage>? additionalMessages = null) {
        var messages = new List<ChatMessage>(additionalMessages ?? []);
        messages.AddRange(ChatMessages.ToList());

        var chatMessages = await AiService.PromptAsync(messages);
        chatMessages = await FitlerMessages(chatMessages);

        foreach(var item in chatMessages) {
            ChatMessages.AddItem(item);
        }

        return chatMessages;
    }

    public async Task<List<ChatMessage>> PromptAsync<Option>(
        ToolsFunctions<Option> toolsFunctions,
        Option option,
        List<ChatMessage>? additionalMessages = null
    ) where Option : BaseOption {
        if(AiService.GetType().GetInterface(nameof(IAiResponseToolService<List<ChatMessage>>)) == null) {
            throw new InvalidOperationException($"{AiService.GetType().Name} does not implement {nameof(IAiResponseToolService<List<ChatMessage>>)}");
        }

        var messages = new List<ChatMessage>(additionalMessages ?? []);
        messages.AddRange(ChatMessages.ToList());

        var chatMessages = await ((IAiResponseToolService<List<ChatMessage>>)AiService).PromptAsync(messages, toolsFunctions, option);
        chatMessages = await FitlerMessages(chatMessages);

        foreach(var item in chatMessages) {
            ChatMessages.AddItem(item);
        }

        return chatMessages;
    }

    private async Task<List<ChatMessage>> FitlerMessages(List<ChatMessage> messages) {
        List<ChatMessage> filteredMessages = new();
        foreach(var message in messages) {
            var textPartIndex = message.Content.ToList().FindIndex(part => part.Text != null);
            if(textPartIndex == -1) continue;

            string textPart = message.Content[textPartIndex].Text!;
            foreach(var filterService in filterServices) {
                textPart = await filterService.FilterAsync(textPart);
            }

            message.Content[textPartIndex] = ChatMessageContentPart.CreateTextPart(textPart);
            filteredMessages.Add(message);
        }

        return filteredMessages;
    }
}