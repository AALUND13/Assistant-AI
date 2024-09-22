using AssistantAI.Services.Interfaces;
using Newtonsoft.Json;
using NLog;

namespace AssistantAI.Services {
    public class JsonDatabaseService<TData> : IDatabaseService<TData>{
        private readonly static Logger _logger = LogManager.GetCurrentClassLogger();
        private readonly string _path = "./data.json";

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public TData Data { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public void LoadDatabase(TData defaultData) {
            if(File.Exists(_path)) {
                try {
                    Data = JsonConvert.DeserializeObject<TData>(File.ReadAllText(_path))!;
                } catch(Exception ex) {
                    _logger.Warn($"Failed to load database from {_path}: {ex.Message}");
                    _logger.Debug("Initializing database with default data.");

                    Data = defaultData;
                }
            } else {
                _logger.Debug("Initializing database with default data.");

                Data = defaultData;
                SaveDatabase(); // Write default data to file if no file exists
            }

            if(Data == null) {
                throw new Exception("Failed to load or initialize database.");
            }
        }

        public void SaveDatabase() {
            File.WriteAllText(_path, JsonConvert.SerializeObject(Data, Formatting.Indented));
        }
    }
}
