using AssistantAI.Services.AI;
using AssistantAI.Services.Interfaces;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using OpenAI.Chat;
using System.Reflection;

namespace AssistantAI.Services {
    public class Data {
        public class UserData {
            public Dictionary<string, long> CommandCooldowns { get; set; } = new();
        }

        public Dictionary<ulong, UserData> Users { get; set; } = new();
    }

    public static class ServiceManager {
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();
        private readonly static ServiceCollection services = new();

        public static IServiceProvider? ServiceProvider { get; private set; }

        public static void InitializeServices() {
            ConfigureServices();
            ServiceProvider = services.BuildServiceProvider();
            logger.Info("Services initialized.");

            IConfigService configService = GetService<IConfigService>();
            configService.LoadConfig();

            IDatabaseService<Data> databaseService = GetService<IDatabaseService<Data>>();
            databaseService.LoadDatabase(new Data());

            InializeDiscordClient();
        }

        public static T GetService<T>() where T : notnull {
            if(ServiceProvider == null) {
                throw new Exception("Service Manager has not been initialized.");
            }

            return ServiceProvider.GetRequiredService<T>();
        }



        private static void InializeDiscordClient() {
            DiscordClient client = GetService<DiscordClient>();
            client.ConnectAsync().Wait();
            logger.Info($"Connected to Discord as {client.CurrentUser.Username}#{client.CurrentUser.Discriminator}");
        }

        private static void ConfigureServices() {
            services.AddLogging(loggerBuilder => {
                loggerBuilder.ClearProviders();
                loggerBuilder.AddNLog();
            });

            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<IDatabaseService<Data>, JsonDatabaseService<Data>>();

            ServiceProvider = services.BuildServiceProvider(); // Create a temporary service provider to get the config service
            IConfigService configService = ServiceProvider.GetRequiredService<IConfigService>();
            configService.LoadConfig();
            logger.Info("Temporary configuration service loaded.");



            logger.Debug("Initializing Discord client services...");
            services.AddDiscordClient(configService.Config.Token, DiscordIntents.All);
            logger.Info("Discord client initialized.");

            services.AddCommandsExtension(
                extension => {

                    extension.AddCommands(Assembly.GetExecutingAssembly());

                    SlashCommandProcessor slashCommandProcessor = new(new());
                    TextCommandProcessor textCommandProcessor = new(new() {
                        PrefixResolver = new DefaultPrefixResolver(true, "a!").ResolvePrefixAsync
                    });

                    extension.AddProcessors(slashCommandProcessor, textCommandProcessor);

                },
                new CommandsConfiguration() {
                    RegisterDefaultCommandProcessors = true,
                }
            );
            logger.Info("Commands initialized.");

            services.AddSingleton<IAiResponseService<AssistantChatMessage>, ReasoningAiService>();
            services.AddSingleton<IAiResponseService<bool>, ReplyDecisionService>();

            logger.Debug("Initializing event handlers...");
            ConfigureEventHandlers();
            logger.Info("Event handlers initialized.");
        }

        private static void ConfigureEventHandlers() {
            List<Type> handlerTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(type => typeof(IEventHandler).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                .ToList();

            logger.Debug($"Found {handlerTypes.Count} event handlers.");

            services.ConfigureEventHandlers(eventHandlingBuilder => {
                foreach(var handlerType in handlerTypes) {
                    // Use reflection to dynamically call the generic AddEventHandlers<T> method
                    var method = eventHandlingBuilder.GetType().GetMethod("AddEventHandlers")!.MakeGenericMethod(handlerType);

                    // Invoke the method dynamically
                    method.Invoke(eventHandlingBuilder, [ServiceLifetime.Singleton]);

                    logger.Debug($"Added event handler: {handlerType.Name}");
                }
            });
        }
    }
}
