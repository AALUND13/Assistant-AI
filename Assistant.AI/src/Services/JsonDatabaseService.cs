using AssistantAI.Services.Interfaces;
using Newtonsoft.Json;
using NLog;

namespace AssistantAI.Services;

public class JsonDatabaseService<TData> : IDatabaseService<TData>{
    private readonly static Logger logger = LogManager.GetCurrentClassLogger();
    private readonly string path = "./data.json";

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public TData Data { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public void LoadDatabase(TData defaultData) {
        if(File.Exists(path)) {
            try {
                Data = JsonConvert.DeserializeObject<TData>(File.ReadAllText(path))!;
            } catch(Exception ex) {
                logger.Warn("Failed to load database from {Path}: {ErrorMessage}", path, ex.Message);

                Data = defaultData;
            }
        } else {
            Data = defaultData;
            SaveDatabase(); // Write default data to file if no file exists
        }

        if(Data == null) {
            throw new Exception("Failed to load or initialize database.");
        }

        logger.Info("Database loaded successfully.");
    }

    public void SaveDatabase() {
        File.WriteAllText(path, JsonConvert.SerializeObject(Data, Formatting.Indented));
    }
}
