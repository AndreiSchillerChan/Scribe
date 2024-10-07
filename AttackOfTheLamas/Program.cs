using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AttackOfTheLamas;

class Program
{
    private static readonly HttpClient Client = new();
    static readonly string BaseUrl = "http://localhost:11434/api/chat";
    static Dictionary<string, List<string>> _fileHistory = new();
    private static List<Message> _conversationHistory = new();
    static bool _exitRequested;
    static bool _isDone;

    private static readonly Dictionary<string, DateTime> LastReadTimes = new();

    static async Task Main(string[] args)
    {
        string directoryToWatch = "C:\\Users\\AndreiSchiller-Chan\\Documents\\Moneybox\\Moneybox.Consumers\\";
        Console.WriteLine($"Monitoring changes in: {directoryToWatch}");

        Task fileWatcherTask = Task.Run(() => SetupFileWatcher(directoryToWatch));

        // Handle user input asynchronously on the main thread
        await HandleUserInput();

        // Wait for the file watcher to complete when the exit is requested
        await fileWatcherTask;
    }

    static void SetupFileWatcher(string path)
    {
        using FileSystemWatcher watcher = new FileSystemWatcher();

        watcher.Path = path;
        watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        watcher.Filter = "*.cs";
        watcher.IncludeSubdirectories = true;

        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
        watcher.Renamed += OnRenamed;
        watcher.EnableRaisingEvents = true;

        // Keep this thread running until explicitly stopped
        while (!_exitRequested)
        {
            Thread.Sleep(1000);
        }
    }

    private static async void OnChanged(object source, FileSystemEventArgs e)
    {
        if (!ShouldProcessFileChange(e.FullPath)) return;
        
        Console.WriteLine($"File: {e.FullPath} {e.ChangeType}");
        await ProcessFileChange(e.FullPath);
    }

    private static async void OnRenamed(object source, RenamedEventArgs e)
    {
        Console.WriteLine($"File: {e.OldFullPath} renamed to {e.FullPath}");
        await ProcessFileChange(e.FullPath);
    }

    static async Task ProcessFileChange(string filePath)
    {
        var fileContent = await File.ReadAllTextAsync(filePath);
        var request = new CodeEvaluationRequest
        {
            CurrentCode = fileContent
        };
        await SendCodeToOllama(request, filePath);
    }
    
    private static bool ShouldProcessFileChange(string filePath)
    {
        const double debounceTime = 1.0; // In seconds
        DateTime lastRead;

        if (LastReadTimes.TryGetValue(filePath, out lastRead))
        {
            if ((DateTime.Now - lastRead).TotalSeconds < debounceTime)
            {
                return false; // Skip this change as it's within the debounce period
            }
        }

        LastReadTimes[filePath] = DateTime.Now;
        return true;
    }

static async Task SendCodeToOllama(CodeEvaluationRequest request, string filePath)
{
    _conversationHistory.Add(new Message
    {
        Role = "user", // Special role to indicate this is a code content
        Content = $"{request.CurrentCode}"
    });
    
    var payload = new
    {
        model = "llama3:latest",
        messages = _conversationHistory,
        stream = true,
        options = new { temperature = 0.3 },
    };

    var options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull 
    };

    var jsonContent = JsonSerializer.Serialize(payload, options);
    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

    var requestMessage = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
    {
        Content = content
    };

    HttpResponseMessage response = await Client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

    if (response.IsSuccessStatusCode)
    {
        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string line;
        int cursorPosition = 0;

        // Clear the current console line to ensure it's clean before starting
        Console.Write("\r");
        StringBuilder fullMessage = new StringBuilder();

        while ((line = await reader.ReadLineAsync()) != null && !_isDone)
        {
            try
            {
                var responseObj = JsonSerializer.Deserialize<StreamResponse>(line, options);
                if (responseObj?.Message != null && !string.IsNullOrEmpty(responseObj.Message.Content))
                {
                    fullMessage.Append(responseObj.Message.Content);
                    Console.Write(responseObj.Message.Content);
                    cursorPosition += responseObj.Message.Content.Length;

                    UpdateFileHistory(responseObj.Message.Content, filePath);
                }

                if (responseObj != null && responseObj.Done)
                {
                    _isDone = true;
                    Console.WriteLine(); // Move to the next line after the message is complete
                    _conversationHistory.Add(new Message
                    {
                        Role = "assistant",
                        Content = fullMessage.ToString()
                    });
                    break;
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Error during deserialization: {ex.Message}");
                Console.Write("\r");
                cursorPosition = 0;
            }
        }

        _isDone = false;
    }
    else
    {
        Console.WriteLine("Error communicating with Ollama: " + response.StatusCode);
    }
}

    static async Task HandleUserInput()
    {
        while (!_exitRequested)
        {
            _isDone = false;
            string input = Console.ReadLine();
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                _exitRequested = true;
            }
            else
            {
                await SendChatToOllama(input);
            }
        }
    }


    static async Task SendChatToOllama(string userInput)
    {
        _conversationHistory.Add(new Message
        {
            Role = "user",
            Content = userInput
        });
        
        var payload = new
        {
            model = "llama3:latest",
            messages = _conversationHistory,
            stream = true, // Enable streaming responses
            options = new { temperature = 0.4 }
        };

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true, // Makes the deserializer ignore case when matching JSON properties to C# properties.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull // Ignores null values when serializing.
        };

        var jsonContent = JsonSerializer.Serialize(payload, options);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Create the request message
        var request = new HttpRequestMessage(HttpMethod.Post, BaseUrl)
        {
            Content = content
        };

        // Send the request with HttpCompletionOption.ResponseHeadersRead to begin streaming
        HttpResponseMessage response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

        if (response.IsSuccessStatusCode)
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            string line;
            int cursorPosition = 0;

            // Clear the current console line to ensure it's clean before starting
            Console.WriteLine();
            Console.Write("\r"); // Carriage return to move to the beginning of the line
            StringBuilder fullMessage = new StringBuilder(); // This will store the entire message
            while ((line = await reader.ReadLineAsync()) != null && !_isDone)
            {
                try
                {
                    var responseObj = JsonSerializer.Deserialize<StreamResponse>(line, options);
                    if (responseObj?.Message != null && !string.IsNullOrEmpty(responseObj.Message.Content))
                    {
                        fullMessage.Append(responseObj.Message.Content);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(responseObj.Message.Content);
                        cursorPosition += responseObj.Message.Content.Length;
                    }

                    // Check if this chunk marks the end of the message
                    if (responseObj is not { Done: true }) 
                        continue;
                    
                    _isDone = true;
                    _conversationHistory.Add(new Message
                    {
                        Role = "assistant",
                        Content = fullMessage.ToString()
                    });
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.ResetColor(); 
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error during deserialization: {ex.Message}");
                    Console.Write("\r");
                    cursorPosition = 0;
                }
            }
        }
        else
        {
            Console.WriteLine("Error communicating with Ollama: " + response.StatusCode);
        }
    }
    
    static void UpdateFileHistory(string result, string filePath)
    {
        // Only update file history if the result pertains to a code evaluation
        if (result.Contains("Refactoring")) // Assuming the API specifies this keyword in relevant messages
        {
            if (!_fileHistory.ContainsKey(filePath))
            {
                _fileHistory[filePath] = new List<string>();
            }
            _fileHistory[filePath].Add(result);
        }

        // Update conversation history regardless
        _conversationHistory.Add(new Message
        {
            Role = "assistant",
            Content = result
        });

        // Provide user feedback based on the content of the message
        if (result.Contains("Good job")) // Adjust this condition based on actual model response
        {
            Console.WriteLine("No refactoring required: Good job!");
        }
        else if (result.Contains("Refactoring"))
        {
            Console.WriteLine("Refactoring suggestions:\n" + result);
        }
    }

    public class StreamResponse
    {
        [JsonPropertyName("model")] public string Model { get; set; }
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("message")] public Message Message { get; set; }
        [JsonPropertyName("done")] public bool Done { get; set; }
        [JsonPropertyName("total_duration")] public long? TotalDuration { get; set; }
        [JsonPropertyName("load_duration")] public long? LoadDuration { get; set; }

        [JsonPropertyName("prompt_eval_count")]
        public int? PromptEvalCount { get; set; }

        [JsonPropertyName("prompt_eval_duration")]
        public long? PromptEvalDuration { get; set; }

        [JsonPropertyName("eval_count")] public int? EvalCount { get; set; }
        [JsonPropertyName("eval_duration")] public long? EvalDuration { get; set; }
    }

    public class Message
    {
        [JsonPropertyName("role")] public string Role { get; set; }
        [JsonPropertyName("content")] public string Content { get; set; }
    }

    public class CodeEvaluationRequest
    {
        public string CurrentCode { get; set; }
        public List<string> PreviousEvaluations { get; set; } = new List<string>();
    }
}