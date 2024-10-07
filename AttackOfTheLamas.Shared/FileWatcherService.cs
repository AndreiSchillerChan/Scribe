using System.Collections.Concurrent;
using AttackOfTheLamas.Shared.Requests;

namespace AttackOfTheLamas.Shared;

public interface IFileWatcherService
{
    void WatchDirectory(string directoryPath);
    void StopWatching();
}

public class FileWatcherService : IFileWatcherService
{
    private readonly IGeminiApiService _geminiApiService;
    private readonly ConcurrentDictionary<string, DateTime> _lastReadTimes = new();
    private bool _exitRequested;
    private FileSystemWatcher _watcher;

    public FileWatcherService(IGeminiApiService geminiApiService)
    {
        _geminiApiService = geminiApiService;
    }

    public void WatchDirectory(string path)
    {
        _watcher = new FileSystemWatcher();

        _watcher.Path = path;
        _watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        _watcher.Filter = "*.cs";
        _watcher.IncludeSubdirectories = true;

        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Renamed += OnRenamed;
        _watcher.EnableRaisingEvents = true;

        // Keep this thread running until explicitly stopped
        var cancellationTokenSource = new CancellationTokenSource();
        Task.Run(async () =>
        {
            while (!_exitRequested)
            {
                await Task.Delay(1000, cancellationTokenSource.Token);
            }
        }, cancellationTokenSource.Token);
    }
    
    private void OnChanged(object source, FileSystemEventArgs e)
    {
        if (!ShouldProcessFileChange(e.FullPath)) return;

        Console.WriteLine($"File: {e.FullPath} {e.ChangeType}");
        ProcessFileChange(e.FullPath);
    }


    private void OnRenamed(object source, RenamedEventArgs e)
    {
        Console.WriteLine($"File: {e.OldFullPath} renamed to {e.FullPath}");
        ProcessFileChange(e.FullPath);
    }

    private async Task ProcessFileChangeAsync(string filePath)
    {
        try
        {
            // Use FileStream with FileShare.ReadWrite to avoid locking issues
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var streamReader = new StreamReader(fileStream))
            {
                var fileContent = await streamReader.ReadToEndAsync();

                var request = new Message
                {
                    Role = "User",
                    Content = fileContent
                };

                // Send the file contents to the Gemini API
                await _geminiApiService.SendCodeToGeminiApi(request);
            }
        }
        catch (IOException ex)
        {
            // Handle file access issues, such as when the file is temporarily locked
            Console.WriteLine($"Error reading file: {filePath}, {ex.Message}");
        }
    }

    private void ProcessFileChange(string filePath)
    {
        Task.Run(async () => await ProcessFileChangeAsync(filePath));
    }
    
    private bool ShouldProcessFileChange(string filePath)
    {
        const double debounceTime = 1.0; // In seconds

        if (_lastReadTimes.TryGetValue(filePath, out var lastRead))
        {
            if ((DateTime.Now - lastRead).TotalSeconds < debounceTime)
            {
                return false; // Skip this change as it's within the debounce period
            }
        }

        _lastReadTimes[filePath] = DateTime.Now;
        return true;
        
    }
    
    public void StopWatching()
    {
        _exitRequested = true;

        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }
    }
}