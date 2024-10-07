using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;

namespace AttackOfTheLamasWeb;

public class OllamaWeb
{
    private readonly IHubContext<ChatHub> _hubContext;
    private static readonly HttpClient Client = new();
    static readonly string BaseUrl = "http://localhost:11434/api/chat";
    static Dictionary<string, List<string>> _fileHistory = new();
    private static List<Message> _conversationHistory = new();
    static bool _exitRequested;
    static bool _isDone;

    private static readonly Dictionary<string, DateTime> LastReadTimes = new();

    public OllamaWeb(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    // static async Task HandleUserInput()
    // {
    //     while (!_exitRequested)
    //     {
    //         _isDone = false;
    //         string input = Console.ReadLine();
    //         if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
    //         {
    //             _exitRequested = true;
    //         }
    //         else
    //         {
    //             await SendChatToOllama(input);
    //         }
    //     }
    // }

    public async Task SendChatToOllama(string userInput)
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
                        await _hubContext.Clients.All.SendAsync("ReceiveMessage", "Assistant", responseObj.Message.Content);

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
}