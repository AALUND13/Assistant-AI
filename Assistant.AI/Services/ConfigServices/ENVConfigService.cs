using AssistantAI.Services.Interfaces;
using NLog;
using System.Reflection;

namespace AssistantAI.Services.ConfigServices;

public class ENVConfigService : IConfigService {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();

    public ConfigStruct Config { get; set; }

    public void LoadConfig() {
        var propertyNames = GetPropertyNames();;
        var properties = new List<string>();

        logger.Info("Loading configuration from environment variables.");
        foreach(var property in propertyNames) {
            if(Environment.GetEnvironmentVariable(property, EnvironmentVariableTarget.User) == null) {
                logger.Warn($"Environment variable {property} not found.");
                continue;
            }
            properties.Add(Environment.GetEnvironmentVariable(property, EnvironmentVariableTarget.User));
        }

        Config = (ConfigStruct)Activator.CreateInstance(typeof(ConfigStruct), properties.ToArray())!;
    }

    private List<string> GetPropertyNames() {
        return typeof(ConfigStruct).GetProperties().Select(prop => prop.Name).ToList();
    }
}
