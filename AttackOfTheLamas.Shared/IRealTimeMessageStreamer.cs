namespace AttackOfTheLamas.Shared;

public interface IRealTimeMessageStreamer
{
    Task StreamMessage(string message);
    event Action<string> OnMessage;
}