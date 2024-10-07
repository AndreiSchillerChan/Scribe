using System.Text.Json.Serialization;

namespace AttackOfTheLamas.Shared.Requests;

public class GenerateContentRequest
{
    [JsonPropertyName("contents")]
    public List<Content> Contents { get; set; }
}

public class Content
{
    [JsonPropertyName("role")]
    public string Role { get; set; }

    [JsonPropertyName("parts")]
    public List<Part> Parts { get; set; }
}

public class Part
{
    [JsonPropertyName("text")]
    public string Text { get; set; }
}