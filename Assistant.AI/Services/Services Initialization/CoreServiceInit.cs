using AssistantAI.Resources;
using AssistantAI.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;

namespace AssistantAI.Services;

public static partial class ServiceManager {
#nullable disable
    public static IServiceProvider ServiceProvider { get; private set; }
#nullable restore

    private readonly static Logger logger = LogManager.GetCurrentClassLogger();
    private readonly static IServiceCollection services = new ServiceCollection();

    public static void InitializeServices() {
        ConfigureServices();
        ServiceProvider = services.BuildServiceProvider();
        logger.Info("Services initialized.");

        IConfigService configService = ServiceProvider.GetRequiredService<IConfigService>();
        configService.LoadConfig();

        using(SqliteDatabaseContext context = ServiceProvider.GetRequiredService<SqliteDatabaseContext>()) {
            context.Database.EnsureCreated();
        }

        InitializeDiscordServices();
    }

    private static void ConfigureServices() {
        services.AddLogging(loggerBuilder => {
            loggerBuilder.ClearProviders();
            loggerBuilder.AddNLog();
        });

        services.AddSingleton<IConfigService, ENVConfigService>();

        ServiceProvider = services.BuildServiceProvider(); // Create a temporary service provider to get the config service
        IConfigService configService = ServiceProvider.GetRequiredService<IConfigService>();
        configService.LoadConfig();
        logger.Info("Temporary configuration service loaded.");

        services.AddTransient<ResourceHandler<Personalitys>>();

        services.AddDbContext<SqliteDatabaseContext>(options =>
            options.UseSqlite("Data Source=database.db"));

        ConfigureAiServices(configService.Config.OPENAI_KEY);
        ConfigureDiscordServices(configService.Config.DISCORD_TOKEN);
    }
}
