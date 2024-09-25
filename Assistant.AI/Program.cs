using AssistantAI.Services;
using AssistantAI.Utilities;
using NLog;

namespace AssistantAI;

class Program {
    static async Task Main() {
        LogManager.Configuration = new NLogConfig().Configuration;
        Logger logger = LogManager.GetCurrentClassLogger();

        AppDomain.CurrentDomain.UnhandledException += (sender, err) => {
            logger.Fatal(err.ExceptionObject.ToString());

            Console.Write("A fatal error occurred. Press any key to exit.");
            Console.ReadKey();
        };

        ServiceManager.InitializeServices();

        //Config.LoadConfig();
        //await DCClient.InitializeAsync();

        // Keep the bot running
        await Task.Delay(-1);
    }
}