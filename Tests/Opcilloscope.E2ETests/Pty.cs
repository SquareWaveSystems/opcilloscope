using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Opcilloscope.E2ETests;

/// <summary>
/// Linux pseudo-terminal process host backed directly by libc.
/// </summary>
public sealed class Pty : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct WinSize
    {
        public ushort Rows;
        public ushort Cols;
        public ushort XPixel;
        public ushort YPixel;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int openpty(
        out int master,
        out int slave,
        IntPtr name,
        IntPtr termios,
        ref WinSize windowSize);

    [DllImport("libc", CharSet = CharSet.Ansi)]
    private static extern int posix_spawn(
        out int processId,
        string path,
        IntPtr fileActions,
        IntPtr attributes,
        string?[] arguments,
        string?[] environment);

    [DllImport("libc")]
    private static extern int posix_spawn_file_actions_init(IntPtr fileActions);

    [DllImport("libc")]
    private static extern int posix_spawn_file_actions_adddup2(IntPtr fileActions, int descriptor, int newDescriptor);

    [DllImport("libc")]
    private static extern int posix_spawn_file_actions_addclose(IntPtr fileActions, int descriptor);

    [DllImport("libc")]
    private static extern int posix_spawn_file_actions_destroy(IntPtr fileActions);

    [DllImport("libc")]
    private static extern int posix_spawnattr_init(IntPtr attributes);

    [DllImport("libc")]
    private static extern int posix_spawnattr_setflags(IntPtr attributes, short flags);

    [DllImport("libc")]
    private static extern int posix_spawnattr_destroy(IntPtr attributes);

    [DllImport("libc", SetLastError = true)]
    private static extern nint read(int descriptor, [Out] byte[] buffer, nuint count);

    [DllImport("libc", SetLastError = true)]
    private static extern unsafe nint write(int descriptor, byte* buffer, nuint count);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int CloseFileDescriptor(int descriptor);

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int processId, int signal);

    [DllImport("libc", SetLastError = true)]
    private static extern int waitpid(int processId, out int status, int options);

    private const int OpaqueStructureSize = 1024;
    private const short PosixSpawnSetSession = 0x80;
    private const int SignalKill = 9;
    private const int SignalTerminate = 15;
    private const int WaitNoHang = 1;
    private const int ErrorInterrupted = 4;
    private const int ErrorNoSuchProcess = 3;
    private const int ErrorNoChild = 10;
    private const int ErrorIo = 5;

    private readonly object _processGate = new();
    private readonly object _writeGate = new();
    private readonly int _master;
    private int _processId;
    private int? _exitCode;
    private volatile bool _disposed;

    private Pty(int master, int processId, int rows, int cols)
    {
        _master = master;
        _processId = processId;
        Rows = rows;
        Cols = cols;
    }

    public int Rows { get; }

    public int Cols { get; }

    public bool HasExited
    {
        get
        {
            lock (_processGate)
            {
                return TryReapNoHang();
            }
        }
    }

    public int? ExitCode
    {
        get
        {
            _ = HasExited;
            lock (_processGate)
            {
                return _exitCode;
            }
        }
    }

    public static Pty Spawn(
        string path,
        IReadOnlyList<string> arguments,
        int rows,
        int cols,
        IReadOnlyDictionary<string, string>? extraEnvironment = null)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("The PTY E2E harness is Linux-only.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(rows, ushort.MaxValue);
        ArgumentOutOfRangeException.ThrowIfLessThan(cols, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cols, ushort.MaxValue);

        var windowSize = new WinSize { Rows = (ushort)rows, Cols = (ushort)cols };
        var master = -1;
        var slave = -1;
        var fileActions = Marshal.AllocHGlobal(OpaqueStructureSize);
        var attributes = Marshal.AllocHGlobal(OpaqueStructureSize);
        var fileActionsInitialized = false;
        var attributesInitialized = false;

        try
        {
            ZeroMemory(fileActions);
            ZeroMemory(attributes);

            if (openpty(out master, out slave, IntPtr.Zero, IntPtr.Zero, ref windowSize) != 0)
            {
                throw LibcFailure("openpty");
            }

            CheckPosixResult(posix_spawn_file_actions_init(fileActions), "posix_spawn_file_actions_init");
            fileActionsInitialized = true;
            CheckPosixResult(posix_spawn_file_actions_adddup2(fileActions, slave, 0), "dup PTY to stdin");
            CheckPosixResult(posix_spawn_file_actions_adddup2(fileActions, slave, 1), "dup PTY to stdout");
            CheckPosixResult(posix_spawn_file_actions_adddup2(fileActions, slave, 2), "dup PTY to stderr");
            CheckPosixResult(posix_spawn_file_actions_addclose(fileActions, master), "close PTY master in child");
            CheckPosixResult(posix_spawn_file_actions_addclose(fileActions, slave), "close original PTY slave in child");

            CheckPosixResult(posix_spawnattr_init(attributes), "posix_spawnattr_init");
            attributesInitialized = true;
            CheckPosixResult(posix_spawnattr_setflags(attributes, PosixSpawnSetSession), "posix_spawnattr_setflags");

            var argumentVector = new List<string?> { path };
            argumentVector.AddRange(arguments);
            argumentVector.Add(null);

            var result = posix_spawn(
                out var processId,
                path,
                fileActions,
                attributes,
                argumentVector.ToArray(),
                BuildEnvironment(extraEnvironment));
            CheckPosixResult(result, $"posix_spawn('{path}')");

            CloseDescriptorNoThrow(slave);
            slave = -1;

            var pty = new Pty(master, processId, rows, cols);
            master = -1;
            return pty;
        }
        finally
        {
            if (fileActionsInitialized)
            {
                CheckPosixResult(posix_spawn_file_actions_destroy(fileActions), "posix_spawn_file_actions_destroy");
            }

            if (attributesInitialized)
            {
                CheckPosixResult(posix_spawnattr_destroy(attributes), "posix_spawnattr_destroy");
            }

            Marshal.FreeHGlobal(fileActions);
            Marshal.FreeHGlobal(attributes);
            CloseDescriptorNoThrow(slave);
            CloseDescriptorNoThrow(master);
        }
    }

    public int Read(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        while (true)
        {
            var count = read(_master, buffer, (nuint)buffer.Length);
            if (count >= 0)
            {
                return checked((int)count);
            }

            var error = Marshal.GetLastPInvokeError();
            if (error == ErrorInterrupted)
            {
                continue;
            }

            if ((_disposed && error != 0) || error == ErrorIo)
            {
                return 0;
            }

            throw LibcFailure("read", error);
        }
    }

    public unsafe void Write(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_writeGate)
        {
            fixed (byte* start = data)
            {
                var offset = 0;
                while (offset < data.Length)
                {
                    var count = write(_master, start + offset, (nuint)(data.Length - offset));
                    if (count > 0)
                    {
                        offset += checked((int)count);
                        continue;
                    }

                    if (count == 0)
                    {
                        throw new IOException("write returned zero before all PTY input was sent.");
                    }

                    var error = Marshal.GetLastPInvokeError();
                    if (error == ErrorInterrupted)
                    {
                        continue;
                    }

                    throw LibcFailure("write", error);
                }
            }
        }
    }

    public bool WaitForExit(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (HasExited)
            {
                return true;
            }

            Thread.Sleep(20);
        }

        return HasExited;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        int processId;
        lock (_processGate)
        {
            _ = TryReapNoHang();
            processId = _processId;
        }

        if (processId != 0)
        {
            SendSignal(processId, SignalTerminate);
            if (!WaitForExit(TimeSpan.FromSeconds(1)))
            {
                SendSignal(processId, SignalKill);
                lock (_processGate)
                {
                    ReapBlocking();
                }
            }
        }

        CloseDescriptorNoThrow(_master);
    }

    private bool TryReapNoHang()
    {
        if (_processId == 0)
        {
            return true;
        }

        while (true)
        {
            var result = waitpid(_processId, out var status, WaitNoHang);
            if (result == 0)
            {
                return false;
            }

            if (result == _processId)
            {
                RecordExit(status);
                return true;
            }

            var error = Marshal.GetLastPInvokeError();
            if (error == ErrorInterrupted)
            {
                continue;
            }

            if (error == ErrorNoChild)
            {
                _processId = 0;
                return true;
            }

            throw LibcFailure("waitpid", error);
        }
    }

    private void ReapBlocking()
    {
        while (_processId != 0)
        {
            var result = waitpid(_processId, out var status, 0);
            if (result == _processId)
            {
                RecordExit(status);
                return;
            }

            var error = Marshal.GetLastPInvokeError();
            if (error == ErrorInterrupted)
            {
                continue;
            }

            if (error == ErrorNoChild)
            {
                _processId = 0;
                return;
            }

            throw LibcFailure("waitpid", error);
        }
    }

    private void RecordExit(int status)
    {
        var signal = status & 0x7f;
        _exitCode = signal == 0
            ? (status >> 8) & 0xff
            : 128 + signal;
        _processId = 0;
    }

    private static void SendSignal(int processId, int signal)
    {
        if (kill(processId, signal) == 0)
        {
            return;
        }

        var error = Marshal.GetLastPInvokeError();
        if (error != ErrorNoSuchProcess)
        {
            throw LibcFailure($"kill({processId}, {signal})", error);
        }
    }

    private static string?[] BuildEnvironment(IReadOnlyDictionary<string, string>? extraEnvironment)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            environment[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
        }

        environment["TERM"] = "xterm-256color";
        if (extraEnvironment is not null)
        {
            foreach (var pair in extraEnvironment)
            {
                environment[pair.Key] = pair.Value;
            }
        }

        var values = environment.Select(pair => (string?)$"{pair.Key}={pair.Value}").ToList();
        values.Add(null);
        return values.ToArray();
    }

    private static void CheckPosixResult(int result, string operation)
    {
        if (result != 0)
        {
            throw new InvalidOperationException($"{operation} failed with error {result}.");
        }
    }

    private static void ZeroMemory(IntPtr pointer)
    {
        for (var index = 0; index < OpaqueStructureSize; index++)
        {
            Marshal.WriteByte(pointer, index, 0);
        }
    }

    private static void CloseDescriptorNoThrow(int descriptor)
    {
        if (descriptor >= 0)
        {
            _ = CloseFileDescriptor(descriptor);
        }
    }

    private static IOException LibcFailure(string operation, int? error = null)
    {
        var errorNumber = error ?? Marshal.GetLastPInvokeError();
        return new IOException($"{operation} failed with errno {errorNumber}.");
    }
}
