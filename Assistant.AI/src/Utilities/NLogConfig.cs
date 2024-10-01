using NLog;
using NLog.Config;
using NLog.Targets;

namespace AssistantAI.Utilities;

public class NLogConfig : ISetupLoadConfigurationBuilder {
    public LogFactory LogFactory { get; set; }
    public LoggingConfiguration Configuration { get; set; }

    public NLogConfig() {
        // Initialize your NLog LogFactory and LoggingConfiguration here
        LogFactory = new LogFactory();
        Configuration = new LoggingConfiguration();

        // Configure NLog targets and rules as needed
        ConfigureLogging();
    }

    private void ConfigureLogging() {
        // Example: Adding a file target
        var fileTarget = new FileTarget("fileTarget") {
            FileName = "${basedir}/logs/${shortdate}.log",
            // Layout includes the stack trace when an exception is logged
            Layout = "${longdate} ${level} ${message} ${exception:format=ToString}"
        };

        Configuration.AddTarget(fileTarget);
        Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, fileTarget));

        var consoleTarget = new ColoredConsoleTarget("consoleTarget") {
            // Layout includes the stack trace when an exception is logged
            Layout = "[${longdate}] [${level}] ${message} ${exception:format=ToString}"
        };

        Configuration.AddTarget(consoleTarget);
        Configuration.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, consoleTarget));
    }
}
