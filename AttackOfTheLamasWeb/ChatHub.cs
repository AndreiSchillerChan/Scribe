using Microsoft.AspNetCore.SignalR;

namespace AttackOfTheLamasWeb;

public class ChatHub : Hub
{
    private readonly OllamaWeb _ollamaWeb;

    public ChatHub(OllamaWeb ollamaWeb)
    {
        _ollamaWeb = ollamaWeb;
    }
    
    public async Task SendMessage(string user, string message)
    {
        await _ollamaWeb.SendChatToOllama(message);
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}