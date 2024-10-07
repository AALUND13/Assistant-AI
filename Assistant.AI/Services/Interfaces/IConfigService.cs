namespace AssistantAI.Services.Interfaces;

public readonly record struct ConfigStruct(string DISCORD_TOKEN, string OPENAI_KEY);

public interface IConfigService {
    ConfigStruct Config { get; }
    void LoadConfig();
}
