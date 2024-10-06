﻿using AssistantAI.AiModule.Services.Default;
using AssistantAI.AiModule.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

namespace AssistantAI.AiModule.Services.Extensions {
    public static class IServiceCollectionExtension {
        public static IServiceCollection AddDefaultAiServices(this IServiceCollection services, string openAiApiKey) {
            services.AddSingleton<IAiResponseToolService<List<ChatMessage>>, ReasoningAiService>();
            services.AddSingleton<IAiResponseService<bool>, DecisionAiService>();

            services.AddSingleton(new ChatClient("gpt-4o-mini", openAiApiKey));

            return services;
        }
    }
}