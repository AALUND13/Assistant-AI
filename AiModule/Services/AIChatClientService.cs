﻿using AssistantAI.AiModule.Services.Interfaces;
using AssistantAI.AiModule.Utilities;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

namespace AssistantAI.AiModule.Services;

public class AIChatClientService {
    public EventList<ChatMessage> ChatMessages = new(50);

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
        AiService = serviceProvider.GetService<IAiResponseToolService<List<ChatMessage>>>() 
            ?? serviceProvider.GetService<IAiResponseService<List<ChatMessage>>>()
            ?? throw new InvalidOperationException("No AI service found");

        filterServices = serviceProvider.GetServices<IFilterService>();
    }

    public async Task<List<ChatMessage>> PromptAsync(List<ChatMessage>? additionalMessages = null) {
        var messages = new List<ChatMessage>(additionalMessages ?? []);
        messages.AddRange(ChatMessages.ToList());

        var chatMessages = await AiService.PromptAsync(messages);
        chatMessages = await FitlerMessages(chatMessages, filterServices);

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
        if(!AiService.GetType().GetInterfaces().Contains(typeof(IAiResponseToolService<List<ChatMessage>>))) {
            throw new InvalidOperationException($"{AiService.GetType().Name} does not implement {nameof(IAiResponseToolService<List<ChatMessage>>)}");
        }

        var messages = new List<ChatMessage>(additionalMessages ?? []);
        messages.AddRange(ChatMessages.ToList());

        var chatMessages = await ((IAiResponseToolService<List<ChatMessage>>)AiService).PromptAsync(messages, toolsFunctions, option);
        chatMessages = await FitlerMessages(chatMessages, filterServices);

        foreach(var message in chatMessages) {
            ChatMessages.AddItem(message);
        }

        return chatMessages;
    }

    public static async Task<List<ChatMessage>> FitlerMessages(List<ChatMessage> messages, IEnumerable<IFilterService> filterServices) {
        List<ChatMessage> filteredMessages = new();
        foreach(var message in messages) {
            var textPartIndex = message.Content.ToList().FindIndex(part => part.Text != null);
            if(textPartIndex == -1) {
                filteredMessages.Add(message);
                continue;
            }

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