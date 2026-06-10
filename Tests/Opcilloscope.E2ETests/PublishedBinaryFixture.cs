using System.Diagnostics;

namespace Opcilloscope.E2ETests;

/// <summary>
/// Provides the path to a published opcilloscope binary for the e2e tests. In CI the path is
/// supplied via the <c>OPCILLOSCOPE_BIN</c> environment variable (the workflow publishes once
/// and reuses it); locally, the fixture publishes a self-contained linux-x64 build on first use.
/// </summary>
public sealed class PublishedBinaryFixture : IAsyncLifetime
{
    public string BinaryPath { get; private set; } = "";

    public Task InitializeAsync()
    {
        var env = Environment.GetEnvironmentVariable("OPCILLOSCOPE_BIN");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        {
            BinaryPath = env;
            return Task.CompletedTask;
        }

        var repoRoot = FindRepoRoot();
        var outDir = Path.Combine(repoRoot, "publish-e2e");
        var binary = Path.Combine(outDir, "opcilloscope");
        if (!File.Exists(binary))
            Publish(repoRoot, outDir);

        if (!File.Exists(binary))
            throw new InvalidOperationException($"Published binary not found at {binary}");
        BinaryPath = binary;
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static void Publish(string repoRoot, string outDir)
    {
        var psi = new ProcessStartInfo("dotnet",
            $"publish \"{Path.Combine(repoRoot, "Opcilloscope.csproj")}\" -c Release -r linux-x64 -o \"{outDir}\"")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException("dotnet publish failed:\n" + p.StandardError.ReadToEnd());
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Opcilloscope.sln")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("Could not locate repo root (Opcilloscope.sln).");
    }
}

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<PublishedBinaryFixture> { }
