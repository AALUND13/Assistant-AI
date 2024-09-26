using AssistantAI.Services.Interfaces;
using DotNetEnv;
using NLog;

namespace AssistantAI.Services;

public class ENVConfigService : IConfigService {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();

    public ConfigStruct Config { get; set; }

    public void LoadConfig() {
        Env.Load();

        var propertyNames = GetPropertyNames(); ;
        var properties = new List<string>();

        logger.Info("Loading configuration from environment variables.");
        foreach(var property in propertyNames) {
            if(Environment.GetEnvironmentVariable(property, EnvironmentVariableTarget.User) != null) {
                properties.Add(Environment.GetEnvironmentVariable(property, EnvironmentVariableTarget.User)!);
            } else if(Env.GetString(property) != null) {
                properties.Add(Env.GetString(property));
            } else {
                logger.Error($"Environment variable {property} not found.");
            }
        }

        Config = (ConfigStruct)Activator.CreateInstance(typeof(ConfigStruct), properties.ToArray())!;
    }

    private List<string> GetPropertyNames() {
        return typeof(ConfigStruct).GetProperties().Select(prop => prop.Name).ToList();
    }
}
