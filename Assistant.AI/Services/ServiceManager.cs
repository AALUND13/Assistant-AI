using AssistantAI.Services.AI;
using AssistantAI.Services.Interfaces;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.EventArgs;
using DSharpPlus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using OpenAI.Chat;
using System.Reflection;

namespace AssistantAI.Services
{
    public class Data {
        public class UserData {
            public Dictionary<string, long> CommandCooldowns { get; set; } = new Dictionary<string, long>();
        }

        public Dictionary<ulong, UserData> Users { get; set; } = new Dictionary<ulong, UserData>();
    }

    public static class ServiceManager {
        private readonly static Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly static ServiceCollection _services = new ServiceCollection();

        public static IServiceProvider? ServiceProvider { get; private set; }

        public static void InitializeServices() {
            ConfigureServices();
            ServiceProvider = _services.BuildServiceProvider();
            _logger.Info("Services initialized.");

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
            _logger.Info($"Connected to Discord as {client.CurrentUser.Username}#{client.CurrentUser.Discriminator}");
        }

        private static void ConfigureServices() {
            _services.AddLogging(loggerBuilder => {
                loggerBuilder.ClearProviders();
                loggerBuilder.AddNLog();
            });

            _services.AddSingleton<IConfigService, ConfigService>();
            _services.AddSingleton<IDatabaseService<Data>, JsonDatabaseService<Data>>();

            ServiceProvider = _services.BuildServiceProvider(); // Create a temporary service provider to get the config service
            IConfigService configService = ServiceProvider.GetRequiredService<IConfigService>();
            configService.LoadConfig();
            _logger.Info("Temporary configuration service loaded.");



            _logger.Debug("Initializing Discord client services...");
            _services.AddDiscordClient(configService.Config.Token, DiscordIntents.All);
            _logger.Info("Discord client initialized.");

            _services.AddCommandsExtension(
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
            _logger.Info("Commands initialized.");

            _services.AddSingleton<IAiResponseService<AssistantChatMessage>, ReasoningAiService>();
            _services.AddSingleton<IAiResponseService<bool>, ReplyDecisionService>();

            _logger.Debug("Initializing event handlers...");
            ConfigureEventHandlers();
            _logger.Info("Event handlers initialized.");
        }

        private static void ConfigureEventHandlers() {
            List<Type> handlerTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(type => typeof(IEventHandler).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                .ToList();

            _logger.Debug($"Found {handlerTypes.Count} event handlers.");

            _services.ConfigureEventHandlers(eventHandlingBuilder => {
                foreach(var handlerType in handlerTypes) {
                    // Use reflection to dynamically call the generic AddEventHandlers<T> method
                    var method = eventHandlingBuilder.GetType().GetMethod("AddEventHandlers")!.MakeGenericMethod(handlerType);

                    // Invoke the method dynamically
                    method.Invoke(eventHandlingBuilder, [ServiceLifetime.Singleton]);

                    _logger.Debug($"Added event handler: {handlerType.Name}");
                }
            });
        }
    }
}
