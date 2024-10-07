using System.Text.Json.Serialization;

namespace AttackOfTheLamas.Shared;

public class Message
{
    [JsonPropertyName("role")] public string Role { get; set; }
    [JsonPropertyName("content")] public string Content { get; set; }
}