﻿using AssistantAI.ContextChecks;
using AssistantAI.Services.AI;
using AssistantAI.Services.Interfaces;
using DSharpPlus;
using DSharpPlus.Commands;
using DSharpPlus.Commands.EventArgs;
using DSharpPlus.Commands.Exceptions;
using DSharpPlus.Commands.Processors.SlashCommands;
using DSharpPlus.Commands.Processors.TextCommands;
using DSharpPlus.Commands.Processors.TextCommands.Parsing;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;
using DSharpPlus.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using OpenAI.Chat;
using System.Reflection;
using System.Text;

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
                        PrefixResolver = new DefaultPrefixResolver(false, "a!").ResolvePrefixAsync
                    });

                    extension.CommandErrored += CommandErrorHandler;

                    extension.AddProcessors(slashCommandProcessor, textCommandProcessor);

                    extension.AddCheck<CooldownCheck>();
                },
                new CommandsConfiguration() {
                    RegisterDefaultCommandProcessors = false,
                    UseDefaultCommandErrorHandler = false
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

        /// <summary>
        /// Modified version of the default CommandErrorHandler to handle exceptions in a more user-friendly way.
        /// Original source: <see href="https://github.com/DSharpPlus/DSharpPlus/blob/a04d686c9a2841401d210844265c85648381615a/DSharpPlus.Commands/CommandsExtension.cs#L490"/>
        /// </summary>
        private static async Task CommandErrorHandler(CommandsExtension extension, CommandErroredEventArgs eventArgs) {
            StringBuilder stringBuilder = new();
            DiscordMessageBuilder messageBuilder = new();

            // Error message
            stringBuilder.Append(eventArgs.Exception switch {
                CommandNotFoundException commandNotFoundException
                    => $"Command {Formatter.InlineCode(Formatter.Sanitize(commandNotFoundException.CommandName))} was not found.",
                ArgumentParseException argumentParseException
                    => $"Failed to parse argument {Formatter.InlineCode(Formatter.Sanitize(argumentParseException.Parameter.Name))}.",
                ChecksFailedException checksFailedException when checksFailedException.Errors.Count == 1
                    => checksFailedException.Errors[0].ErrorMessage,
                ChecksFailedException checksFailedException
                    => $"The following context checks failed: {Formatter.InlineCode(Formatter.Sanitize(string.Join("\n\n", checksFailedException.Errors.Select(x => x.ErrorMessage))))}.",
                ParameterChecksFailedException checksFailedException when checksFailedException.Errors.Count == 1
                    => $"The following error occurred: {Formatter.InlineCode(Formatter.Sanitize(checksFailedException.Errors[0].ErrorMessage))}",
                ParameterChecksFailedException checksFailedException
                    => $"The following context checks failed: {Formatter.InlineCode(Formatter.Sanitize(string.Join("\n\n", checksFailedException.Errors.Select(x => x.ErrorMessage))))}.",
                DiscordException discordException when discordException.Response is not null
                    && (int)discordException.Response.StatusCode >= 500
                    && (int)discordException.Response.StatusCode < 600
                    => $"Discord API error {discordException.Response.StatusCode} occurred: {discordException.JsonMessage ?? "No further information was provided."}",
                DiscordException discordException when discordException.Response is not null
                => $"Discord API error {discordException.Response.StatusCode} occurred: {discordException.JsonMessage ?? discordException.Message}",
                _ => $"An unexpected error occurred: {eventArgs.Exception.Message}"
            });

            // Stack trace
            if(!string.IsNullOrWhiteSpace(eventArgs.Exception.StackTrace)) {
                // If the stack trace can fit inside a codeblock
                if(8 + eventArgs.Exception.StackTrace.Length + stringBuilder.Length <= 2000) {
                    stringBuilder.Append($"```\n{eventArgs.Exception.StackTrace}\n```");
                    messageBuilder.WithContent(stringBuilder.ToString());
                }
                // If the exception message exceeds the message character limit, cram it all into an attatched file with a simple message in the content.
                else if(stringBuilder.Length >= 2000) {
                    messageBuilder.WithContent("Exception Message exceeds character limit, see attached file.");
                    string formattedFile = $"{stringBuilder}{Environment.NewLine}{Environment.NewLine}Stack Trace:{Environment.NewLine}{eventArgs.Exception.StackTrace}";
                    messageBuilder.AddFile("MessageAndStackTrace.txt", new MemoryStream(Encoding.UTF8.GetBytes(formattedFile)), AddFileOptions.CloseStream);
                }
                // Otherwise, display the exception message in the content and the trace in an attached file
                else {
                    messageBuilder.WithContent(stringBuilder.ToString());
                    messageBuilder.AddFile("StackTrace.txt", new MemoryStream(Encoding.UTF8.GetBytes(eventArgs.Exception.StackTrace)), AddFileOptions.CloseStream);
                }
            }
            // If no stack trace, and the message is still too long, attatch a file with the message and use a simple message in the content.
            else if(stringBuilder.Length >= 2000) {
                messageBuilder.WithContent("Exception Message exceeds character limit, see attached file.");
                messageBuilder.AddFile("Message.txt", new MemoryStream(Encoding.UTF8.GetBytes(stringBuilder.ToString())), AddFileOptions.CloseStream);
            }
            // Otherwise, if no stack trace and the Exception message will fit, send the message as content
            else {
                messageBuilder.WithContent(stringBuilder.ToString());
            }


            if(eventArgs.Context is SlashCommandContext { Interaction.ResponseState: not DiscordInteractionResponseState.Unacknowledged }) {
                await eventArgs.Context.FollowupAsync(messageBuilder);
            } else {
                await eventArgs.Context.RespondAsync(messageBuilder);
            }
        }

    }
}
