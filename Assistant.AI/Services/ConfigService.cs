using AssistantAI.Services.Interfaces;
using Newtonsoft.Json;
using NLog;

namespace AssistantAI.Services;

public class ConfigService : IConfigService {
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();

    public ConfigStruct Config { get; private set; }

    public void LoadConfig() {
        string execPath = AppDomain.CurrentDomain.BaseDirectory;
        string configPath = Path.Combine(execPath, "config.json");
        logger.Debug($"Loading configuration from {configPath}");

        if(!File.Exists(configPath)) {
            throw new FileNotFoundException("Configuration file not found.", configPath);
        }

        string configJson = File.ReadAllText(configPath);
        Config = JsonConvert.DeserializeObject<ConfigStruct>(configJson);

        logger.Info("Configuration loaded successfully.");
    }
}
