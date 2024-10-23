using AssistantAI.AiModule.Services.Default;
using AssistantAI.AiModule.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Chat;

namespace AssistantAI.Services {
    public static partial class ServiceManager {
        private static void ConfigureAiServices(string openAiKey) {
            services.AddSingleton<IAiResponseToolService<List<ChatMessage>>, NormalResponseService>();
            services.AddSingleton<IAiResponseService<bool>, DecisionAiService>();

            services.Configure<OpenAiConfiguration>(config => {
                config.ApiKey = openAiKey;
            });

            services.AddSingleton<IFilterService, AIFilterService>();
        }
    }
}
