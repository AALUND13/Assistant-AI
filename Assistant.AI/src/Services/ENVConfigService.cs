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
        var properties = new List<object>();

        logger.Info("Loading configuration from environment variables.");
        foreach(var property in propertyNames) {
            if(Environment.GetEnvironmentVariable(property, EnvironmentVariableTarget.Process) != null) {
                properties.Add(GetPropertyValueByType(property));
            } else {
                logger.Error($"Environment variable {property} not found.");
            }
        }

        Config = (ConfigStruct)Activator.CreateInstance(typeof(ConfigStruct), properties.ToArray())!;
    }

    private object GetPropertyValueByType(string propertyName) {
        var typeMapping = new Dictionary<Type, Func<string, object>> {
            { typeof(string), value => Env.GetString(value) },
            { typeof(double), value => Env.GetDouble(value) },
            { typeof(bool), value => Env.GetBool(value) },
            { typeof(int), value => Env.GetInt(value) },
        };

        Type type = typeof(ConfigStruct).GetProperty(propertyName)!.PropertyType;

        if(!typeMapping.ContainsKey(type))
            throw new NotSupportedException($"Type {type} is not supported.");

        return typeMapping[type](propertyName);
    }

    private List<string> GetPropertyNames() {
        return typeof(ConfigStruct).GetProperties().Select(prop => prop.Name).ToList();
    }
}
