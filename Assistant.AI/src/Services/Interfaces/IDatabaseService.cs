namespace AssistantAI.Services.Interfaces;

public interface IDatabaseService<TData>
{
    TData Data { get; }
    void LoadDatabase(TData defaultData);
    void SaveDatabase();
}
