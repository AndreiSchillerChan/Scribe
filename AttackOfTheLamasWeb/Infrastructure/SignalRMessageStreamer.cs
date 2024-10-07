using AttackOfTheLamas.Shared;

namespace AttackOfTheLamasWeb.Infrastructure;

public class RealTimeMessageStreamer : IRealTimeMessageStreamer
{
    public event Action<string> OnMessage;

    public async Task StreamMessage(string message)
    {
        OnMessage?.Invoke(message);
        await Task.CompletedTask;
    }
}

