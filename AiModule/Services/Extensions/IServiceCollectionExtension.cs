using AssistantAI.AiModule.Services.Default;
using AssistantAI.AiModule.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

namespace AssistantAI.AiModule.Services.Extensions {
    public static class IServiceCollectionExtension {
        public static IServiceCollection AddAiModule<TAiModule, TResponseType>(this IServiceCollection services, string? openAIKey = null)
            where TAiModule : class, IAIModule<TResponseType> {

            services.AddTransient<IAIModule<TResponseType>, TAiModule>();

            if(openAIKey != null && !services.Any(service => service.ServiceType == typeof(ChatClient))) {
                services.AddSingleton(new ChatClient("gpt-4o-mini", openAIKey));
            }

            return services;
        }

        public static IServiceCollection AddDefaultAiServices(this IServiceCollection services, string openAiApiKey) {
            services.AddAiModule<ReasoningAiService, List<ChatMessage>>();
            services.AddAiModule<DecisionAiService, bool>();

            services.AddSingleton(new ChatClient("gpt-4o-mini", openAiApiKey));

            return services;
        }
    }
}
