using System.Diagnostics;

namespace Opcilloscope.E2ETests;

/// <summary>
/// Supplies the exact CI artifact or creates a fresh local publish in a temporary directory.
/// </summary>
public sealed class PublishedBinaryFixture : IAsyncLifetime
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromMinutes(5);
    private string? _ownedPublishDirectory;

    public string BinaryPath { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Opcilloscope PTY E2E tests run on Linux only.");
        }

        var configuredBinary = Environment.GetEnvironmentVariable("OPCILLOSCOPE_BIN");
        if (configuredBinary is not null)
        {
            if (string.IsNullOrWhiteSpace(configuredBinary) || !File.Exists(configuredBinary))
            {
                throw new FileNotFoundException(
                    "OPCILLOSCOPE_BIN was set, but the published binary does not exist.",
                    configuredBinary);
            }

            BinaryPath = Path.GetFullPath(configuredBinary);
            return;
        }

        var repositoryRoot = FindRepositoryRoot();
        _ownedPublishDirectory = Path.Combine(
            Path.GetTempPath(),
            $"opcilloscope-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_ownedPublishDirectory);

        try
        {
            await PublishAsync(repositoryRoot, _ownedPublishDirectory);
            BinaryPath = Path.Combine(_ownedPublishDirectory, "opcilloscope");
            if (!File.Exists(BinaryPath))
            {
                throw new FileNotFoundException("The fresh publish did not produce opcilloscope.", BinaryPath);
            }
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (_ownedPublishDirectory is null)
        {
            return;
        }

        var directory = _ownedPublishDirectory;
        _ownedPublishDirectory = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(100);
            }
        }
    }

    private static async Task PublishAsync(string repositoryRoot, string outputDirectory)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("publish");
        startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "Opcilloscope.csproj"));
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("--runtime");
        startInfo.ArgumentList.Add("linux-x64");
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputDirectory);
        startInfo.ArgumentList.Add("-p:DebugType=none");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start dotnet publish.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(PublishTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync();
            var timedOutOutput = await standardOutput;
            var timedOutError = await standardError;
            throw new TimeoutException(
                $"dotnet publish exceeded {PublishTimeout}.\nstdout:\n{timedOutOutput}\nstderr:\n{timedOutError}");
        }

        var output = await standardOutput;
        var error = await standardError;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"dotnet publish exited with code {process.ExitCode}.\nstdout:\n{output}\nstderr:\n{error}");
        }
    }

    private static string FindRepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "Opcilloscope.sln")))
        {
            directory = Directory.GetParent(directory)?.FullName;
        }

        return directory
            ?? throw new InvalidOperationException("Could not locate the repository root from the E2E test output.");
    }
}

[CollectionDefinition("E2E", DisableParallelization = true)]
public sealed class E2ECollection : ICollectionFixture<PublishedBinaryFixture>;
