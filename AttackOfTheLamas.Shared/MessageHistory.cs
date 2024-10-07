namespace AttackOfTheLamas.Shared;

public interface IMessageHistory
{
    event Action<string> OnFileHistoryUpdated;
    void AddUserMessage(string content);
    void AddAssistantMessage(string content);
    void UpdateFileHistory(string result, string filePath);
    string GetLatestFileContent(string filePath); // New method to retrieve the latest content
    string GetLatestAssistantMessage(); // New method to retrieve the latest assistant message
}

public class MessageHistory : IMessageHistory
{
    public event Action<string>? OnFileHistoryUpdated; // Event to notify when file history is updated

    public List<Message> ConversationHistory { get; } = new List<Message>();
    private readonly Dictionary<string, List<string>> _fileHistory = new();

    public void AddUserMessage(string content)
    {
        ConversationHistory.Add(new Message
        {
            Role = "user",
            Content = content
        });
    }

    public void AddAssistantMessage(string content)
    {
        ConversationHistory.Add(new Message
        {
            Role = "assistant",
            Content = content
        });
    }

    public void UpdateFileHistory(string result, string filePath)
    {
        // Ensure the file path has an entry in the dictionary
        if (!_fileHistory.ContainsKey(filePath))
        {
            _fileHistory[filePath] = new List<string>();
        }

        // Add the result to the file history for the given file path
        _fileHistory[filePath].Add(result);
    
        // Fire the event to notify listeners that the file history has been updated
        OnFileHistoryUpdated?.Invoke(filePath);
    }
    public string GetLatestFileContent(string filePath)
    {
        if (_fileHistory.ContainsKey(filePath) && _fileHistory[filePath].Any())
        {
            return _fileHistory[filePath].Last(); // Return the latest content for the file
        }
        return string.Empty; // Return empty if no history for the file
    }
    
    // New method to get the latest assistant message from the conversation history
    public string GetLatestAssistantMessage()
    {
        var lastAssistantMessage = ConversationHistory
            .LastOrDefault(m => m.Role == "model");

        return lastAssistantMessage?.Content ?? string.Empty;
    }
}