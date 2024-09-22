namespace AssistantAI.Services.Interfaces {
    public struct ConfigStruct {
        public string Token;
        public string OpenAIKey;
    }

    public interface IConfigService {
        ConfigStruct Config { get; }
        void LoadConfig();
    }
}
