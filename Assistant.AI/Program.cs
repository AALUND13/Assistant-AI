using AssistantAI.Services;
using NLog;

Logger logger = LogManager.GetCurrentClassLogger();

AppDomain.CurrentDomain.UnhandledException += (sender, err) => {
    logger.Fatal(err.ExceptionObject.ToString());

    Console.Write("A fatal error occurred. Press any key to exit.");
    Console.ReadKey();
};

ServiceManager.InitializeServices();

await Task.Delay(-1);