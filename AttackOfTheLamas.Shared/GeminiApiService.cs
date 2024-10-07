using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AttackOfTheLamas.Shared.Responses.Gemini;

namespace AttackOfTheLamas.Shared;

public interface IGeminiApiService
{
    Task SendCodeToGeminiApi(Message request);
}

public class GeminiApiService : IGeminiApiService
{
    private readonly IRealTimeMessageStreamer _messageStreamer;

    private readonly HttpClient _client;
    private static List<Message> _conversationHistory = new();
    private bool _isDone;
    private readonly string _apiKey; 

    public GeminiApiService(HttpClient client, string apiKey, IRealTimeMessageStreamer messageStreamer)
    {
        _client = client;
        _apiKey = apiKey;
        _messageStreamer = messageStreamer;
    }

    public async Task SendCodeToGeminiApi(Message request)
    {
        _conversationHistory.Add(new Message
        {
            Role = request.Role,
            Content = request.Content
        });

        var payload = new
        {
            contents = _conversationHistory.Select(message => new
            {
                role = message.Role,
                parts = new[]
                {
                    new { text = message.Content }
                }
            }).ToArray()
        };

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var jsonContent = JsonSerializer.Serialize(payload, options);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var requestUrl = $"?key={_apiKey}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUrl)
        {
            Content = content
        };

        var response = await _client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead);

        if (response.IsSuccessStatusCode)
        {
            try
            {
                using var stream = await response.Content.ReadAsStreamAsync();
                // Deserialize the response directly into GeminiApiResponse
                var geminiResponse = await JsonSerializer.DeserializeAsync<GeminiApiResponse>(stream, options);

                if (geminiResponse != null && geminiResponse.Candidates != null)
                {
                    // Iterate over candidates and extract content parts
                    foreach (var candidate in geminiResponse.Candidates)
                    {
                        foreach (var part in candidate.Content.Parts)
                        {
                            var fullText = part.Text;
                            Console.WriteLine(part.Text);
                            var words = fullText.Split(' ');

                            // Stream each word to update Blazor UI
                            foreach (var word in words)
                            {
                                await _messageStreamer.StreamMessage(word + " "); // Send word by word with space
                            }

                            // Add the full message to the conversation history
                            _conversationHistory.Add(new Message
                            {
                                Role = "model",
                                Content = fullText
                            });
                        }

                        // Stop the loop if the finish reason is STOP
                        if (candidate.FinishReason == "STOP")
                        {
                            _isDone = true;
                            break;
                        }
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"Network error: {ex.Message}");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Deserialization error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
            }
            finally
            {
                _isDone = false;
            }
        }
        else
        {
            // Log failure details to the console
            Console.WriteLine($"Request failed with status code: {response.StatusCode}");
            var errorContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Error message: {errorContent}");
        }
    }
}