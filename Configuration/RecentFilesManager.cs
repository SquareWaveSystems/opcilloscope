using System.Text.Json;

namespace OpcScope.Configuration;

/// <summary>
/// Manages the list of recently opened configuration files.
/// </summary>
public class RecentFilesManager
{
    private const int MaxRecentFiles = 10;
    private readonly string _settingsPath;
    private List<string> _recentFiles = new();

    /// <summary>
    /// Event fired when the recent files list changes.
    /// </summary>
    public event Action? FilesChanged;

    public RecentFilesManager()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpcScope",
            "recent-files.json"
        );
        Load();
    }

    /// <summary>
    /// Gets the list of recent file paths.
    /// </summary>
    public IReadOnlyList<string> Files => _recentFiles.AsReadOnly();

    /// <summary>
    /// Adds a file path to the recent files list.
    /// If the file already exists in the list, it's moved to the front.
    /// </summary>
    /// <param name="filePath">The file path to add.</param>
    public void Add(string filePath)
    {
        // Normalize the path
        filePath = Path.GetFullPath(filePath);

        // Remove if exists (will be re-added at front)
        _recentFiles.Remove(filePath);

        // Add to front of list
        _recentFiles.Insert(0, filePath);

        // Trim to max size
        if (_recentFiles.Count > MaxRecentFiles)
            _recentFiles = _recentFiles.Take(MaxRecentFiles).ToList();

        Save();
        FilesChanged?.Invoke();
    }

    /// <summary>
    /// Removes a file path from the recent files list.
    /// </summary>
    /// <param name="filePath">The file path to remove.</param>
    public void Remove(string filePath)
    {
        filePath = Path.GetFullPath(filePath);
        if (_recentFiles.Remove(filePath))
        {
            Save();
            FilesChanged?.Invoke();
        }
    }

    /// <summary>
    /// Clears all recent files.
    /// </summary>
    public void Clear()
    {
        if (_recentFiles.Count > 0)
        {
            _recentFiles.Clear();
            Save();
            FilesChanged?.Invoke();
        }
    }

    /// <summary>
    /// Gets only the file paths that still exist on disk.
    /// </summary>
    /// <returns>List of existing file paths.</returns>
    public IReadOnlyList<string> GetExistingFiles()
    {
        return _recentFiles.Where(File.Exists).ToList().AsReadOnly();
    }

    /// <summary>
    /// Removes any files from the list that no longer exist on disk.
    /// </summary>
    public void CleanupMissingFiles()
    {
        var originalCount = _recentFiles.Count;
        _recentFiles = _recentFiles.Where(File.Exists).ToList();

        if (_recentFiles.Count != originalCount)
        {
            Save();
            FilesChanged?.Invoke();
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _recentFiles = JsonSerializer.Deserialize<List<string>>(json) ?? new();
            }
        }
        catch
        {
            // Ignore corrupted settings, start fresh
            _recentFiles = new();
        }
    }

    private void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(_recentFiles));
        }
        catch
        {
            // Ignore save failures - non-critical feature
        }
    }
}
