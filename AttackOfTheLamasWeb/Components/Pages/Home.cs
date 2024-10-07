using Markdig;

namespace AttackOfTheLamasWeb.Components.Pages;

public partial class Home
{
    private readonly string _filePath = "C:\\Users\\AndreiSchiller-Chan\\Documents\\LeetcodeAlgos\\LeetcodeAlgos\\";
    private string _fileContent = "";
    private string ResponseText { get; set; } = "";
    private int CurrentCount { get; set; }
    
    private static bool _isWatcherRunning;
    private MarkdownPipeline? _pipeline;

    protected override void OnInitialized()
    {
        _pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        
        MessageHistory.OnFileHistoryUpdated += HandleFileHistoryUpdated;
        
        _ = StartFileWatcherInBackground();
        
        MessageStreamer.OnMessage += HandleMessage;
    }

    private async void HandleMessage(string message)
    {
        await InvokeAsync(() =>
        {
            ResponseText += message;
            CurrentCount++;
            StateHasChanged();
        });
    }
    
    private Task StartFileWatcherInBackground()
    {
        if (_isWatcherRunning)
            return Task.CompletedTask;

        _isWatcherRunning = true;
        
        return Task.Run(async () =>
        {
            FileWatcherService.WatchDirectory(_filePath);
            await Task.Delay(Timeout.Infinite);
        });
    }

    private async Task StartFileWatcher()
    {
        FileWatcherService.WatchDirectory(_filePath);
        await InvokeAsync(StateHasChanged);
    }

    private void HandleFileHistoryUpdated(string filePath)
    {
        _fileContent = MessageHistory.GetLatestFileContent(filePath);
        InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        MessageHistory.OnFileHistoryUpdated -= HandleFileHistoryUpdated;
        MessageStreamer.OnMessage -= HandleMessage;
        FileWatcherService.StopWatching();
    }
    
    private string ConvertMarkdownToHtml(string markdownText)
    {
        return Markdown.ToHtml(markdownText, _pipeline);
    }
}