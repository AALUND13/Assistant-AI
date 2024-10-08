namespace AssistantAI.AiModule.Services.Interfaces;

public interface IFilterService {
    /// <summary>
    /// This method will modify the message to filter out any unwanted content.
    /// </summary>
    /// <param name="message">The message to filter.</param>
    /// <returns>The modified message.</returns>
    public Task<string> FilterAsync(string message);
}
