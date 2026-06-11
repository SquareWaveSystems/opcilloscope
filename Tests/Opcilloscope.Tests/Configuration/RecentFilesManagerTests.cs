using Opcilloscope.Configuration;

namespace Opcilloscope.Tests.Configuration;

/// <summary>
/// Tests for RecentFilesManager covering add/remove semantics, the bounded cap,
/// and persistence across instances. Uses an isolated base directory via the
/// test-only constructor seam so no real user settings are touched.
/// </summary>
public class RecentFilesManagerTests : IDisposable
{
    private readonly string _baseDir;

    public RecentFilesManagerTests()
    {
        _baseDir = Path.Combine(Path.GetTempPath(), $"OpcilloscopeRecentTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_baseDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_baseDir))
            {
                Directory.Delete(_baseDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }

    private RecentFilesManager CreateManager() => new(_baseDir);

    private string MakePath(string name) => Path.Combine(_baseDir, name);

    [Fact]
    public void Add_AddsFileToFront()
    {
        var manager = CreateManager();

        manager.Add(MakePath("a.cfg"));
        manager.Add(MakePath("b.cfg"));

        Assert.Equal(2, manager.Files.Count);
        Assert.Equal(Path.GetFullPath(MakePath("b.cfg")), manager.Files[0]);
        Assert.Equal(Path.GetFullPath(MakePath("a.cfg")), manager.Files[1]);
    }

    [Fact]
    public void Add_ExistingFile_MovesToFront()
    {
        var manager = CreateManager();

        manager.Add(MakePath("a.cfg"));
        manager.Add(MakePath("b.cfg"));
        manager.Add(MakePath("a.cfg"));

        Assert.Equal(2, manager.Files.Count);
        Assert.Equal(Path.GetFullPath(MakePath("a.cfg")), manager.Files[0]);
    }

    [Fact]
    public void Add_NormalizesPaths()
    {
        var manager = CreateManager();

        manager.Add(MakePath("sub/../a.cfg"));

        Assert.Single(manager.Files);
        Assert.Equal(Path.GetFullPath(MakePath("a.cfg")), manager.Files[0]);
    }

    [Fact]
    public void Add_FiresFilesChanged()
    {
        var manager = CreateManager();
        var fired = false;
        manager.FilesChanged += () => fired = true;

        manager.Add(MakePath("a.cfg"));

        Assert.True(fired);
    }

    [Fact]
    public void Add_BeyondCap_KeepsOnlyTenMostRecent()
    {
        var manager = CreateManager();

        for (var i = 0; i < 15; i++)
        {
            manager.Add(MakePath($"file{i}.cfg"));
        }

        Assert.Equal(10, manager.Files.Count);
        // Most recently added stays at the front.
        Assert.Equal(Path.GetFullPath(MakePath("file14.cfg")), manager.Files[0]);
        // The oldest entries (file0..file4) are evicted; file5 is the oldest kept.
        Assert.Equal(Path.GetFullPath(MakePath("file5.cfg")), manager.Files[9]);
    }

    [Fact]
    public void Remove_RemovesFile()
    {
        var manager = CreateManager();
        manager.Add(MakePath("a.cfg"));
        manager.Add(MakePath("b.cfg"));

        manager.Remove(MakePath("a.cfg"));

        Assert.Single(manager.Files);
        Assert.Equal(Path.GetFullPath(MakePath("b.cfg")), manager.Files[0]);
    }

    [Fact]
    public void Clear_RemovesAllFiles()
    {
        var manager = CreateManager();
        manager.Add(MakePath("a.cfg"));
        manager.Add(MakePath("b.cfg"));

        manager.Clear();

        Assert.Empty(manager.Files);
    }

    [Fact]
    public void Add_PersistsAcrossInstances()
    {
        var manager = CreateManager();
        manager.Add(MakePath("a.cfg"));
        manager.Add(MakePath("b.cfg"));

        // A fresh manager pointed at the same base directory must read the list back.
        var reloaded = CreateManager();

        Assert.Equal(2, reloaded.Files.Count);
        Assert.Equal(Path.GetFullPath(MakePath("b.cfg")), reloaded.Files[0]);
        Assert.Equal(Path.GetFullPath(MakePath("a.cfg")), reloaded.Files[1]);
    }

    [Fact]
    public void Clear_PersistsAcrossInstances()
    {
        var manager = CreateManager();
        manager.Add(MakePath("a.cfg"));
        manager.Clear();

        var reloaded = CreateManager();

        Assert.Empty(reloaded.Files);
    }

    [Fact]
    public void GetExistingFiles_ReturnsOnlyFilesOnDisk()
    {
        var manager = CreateManager();
        var existing = MakePath("exists.cfg");
        File.WriteAllText(existing, "{}");
        manager.Add(existing);
        manager.Add(MakePath("missing.cfg"));

        var result = manager.GetExistingFiles();

        Assert.Single(result);
        Assert.Equal(Path.GetFullPath(existing), result[0]);
    }
}
