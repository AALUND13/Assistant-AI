namespace AssistantAI.Services.Interfaces {
    public interface IFilterService {
        public Task<string> FilterAsync(string message);
    }
}
