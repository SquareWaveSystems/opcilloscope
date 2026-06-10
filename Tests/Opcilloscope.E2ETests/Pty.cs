using System.Runtime.InteropServices;

namespace Opcilloscope.E2ETests;

/// <summary>
/// A pseudo-terminal that runs a child process attached to a sized PTY, implemented purely
/// with libc P/Invoke (no NuGet PTY library, no Node/Python). Linux-only — which is all the
/// CI runners and the dev environment need, and the only place the TUI runs anyway.
///
/// We allocate the PTY ourselves (rather than shelling out to <c>script</c>) because
/// <c>script</c> produces a 0x0 window when its stdout is a pipe, and Terminal.Gui refuses to
/// draw without a known size. <see cref="openpty"/> lets us fix the rows/cols up front.
/// </summary>
public sealed class Pty : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct WinSize { public ushort Rows, Cols, XPixel, YPixel; }

    [DllImport("libc", SetLastError = true)]
    private static extern int openpty(out int master, out int slave, IntPtr name, IntPtr termios, ref WinSize win);

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_spawn(out int pid, string path, IntPtr fileActions, IntPtr attr, string?[] argv, string?[] envp);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_init(IntPtr fa);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_adddup2(IntPtr fa, int fd, int newFd);
    [DllImport("libc")] private static extern int posix_spawn_file_actions_addclose(IntPtr fa, int fd);
    [DllImport("libc")] private static extern int posix_spawnattr_init(IntPtr attr);
    [DllImport("libc")] private static extern int posix_spawnattr_setflags(IntPtr attr, short flags);

    [DllImport("libc", SetLastError = true)] private static extern long read(int fd, byte[] buf, long count);
    [DllImport("libc", SetLastError = true)] private static extern long write(int fd, byte[] buf, long count);
    [DllImport("libc")] private static extern int close(int fd);
    [DllImport("libc")] private static extern int kill(int pid, int sig);
    [DllImport("libc")] private static extern int waitpid(int pid, out int status, int options);

    // glibc posix_spawnattr flag: create a new session for the child (POSIX_SPAWN_SETSID),
    // so the slave PTY becomes its controlling terminal.
    private const short POSIX_SPAWN_SETSID = 0x80;
    private const int SIGTERM = 15;
    private const int WNOHANG = 1;

    private readonly int _master;
    private int _pid;
    private bool _disposed;

    public int Rows { get; }
    public int Cols { get; }

    private Pty(int master, int pid, int rows, int cols)
    {
        _master = master;
        _pid = pid;
        Rows = rows;
        Cols = cols;
    }

    /// <summary>Spawns <paramref name="path"/> attached to a fresh rows x cols PTY.</summary>
    public static Pty Spawn(string path, IReadOnlyList<string> args, int rows, int cols, IReadOnlyDictionary<string, string>? extraEnv = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            throw new PlatformNotSupportedException("The PTY e2e harness is Linux-only.");

        var win = new WinSize { Rows = (ushort)rows, Cols = (ushort)cols };
        if (openpty(out int master, out int slave, IntPtr.Zero, IntPtr.Zero, ref win) != 0)
            throw new InvalidOperationException($"openpty failed (errno {Marshal.GetLastWin32Error()})");

        IntPtr fa = Marshal.AllocHGlobal(1024);
        IntPtr attr = Marshal.AllocHGlobal(1024);
        try
        {
            // Zero the opaque structs (over-allocated to be safe across libc versions).
            for (int i = 0; i < 1024; i++) { Marshal.WriteByte(fa, i, 0); Marshal.WriteByte(attr, i, 0); }

            posix_spawn_file_actions_init(fa);
            posix_spawn_file_actions_adddup2(fa, slave, 0);
            posix_spawn_file_actions_adddup2(fa, slave, 1);
            posix_spawn_file_actions_adddup2(fa, slave, 2);
            posix_spawn_file_actions_addclose(fa, master);
            posix_spawn_file_actions_addclose(fa, slave);

            posix_spawnattr_init(attr);
            posix_spawnattr_setflags(attr, POSIX_SPAWN_SETSID);

            var argv = new List<string?> { path };
            argv.AddRange(args);
            argv.Add(null);

            var envp = BuildEnv(extraEnv);

            int rc = posix_spawn(out int pid, path, fa, attr, argv.ToArray(), envp);
            if (rc != 0)
                throw new InvalidOperationException($"posix_spawn('{path}') failed (rc {rc})");

            close(slave); // parent keeps only the master end
            return new Pty(master, pid, rows, cols);
        }
        finally
        {
            Marshal.FreeHGlobal(fa);
            Marshal.FreeHGlobal(attr);
        }
    }

    private static string?[] BuildEnv(IReadOnlyDictionary<string, string>? extra)
    {
        var env = new Dictionary<string, string>();
        foreach (System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables())
            env[(string)e.Key] = (string?)e.Value ?? "";
        env["TERM"] = "xterm-256color";
        if (extra != null)
            foreach (var kv in extra) env[kv.Key] = kv.Value;

        var list = env.Select(kv => (string?)$"{kv.Key}={kv.Value}").ToList();
        list.Add(null);
        return list.ToArray();
    }

    /// <summary>Reads up to <paramref name="buffer"/>.Length bytes (blocking). Returns 0 at EOF.</summary>
    public int Read(byte[] buffer)
    {
        long n = read(_master, buffer, buffer.Length);
        return n < 0 ? 0 : (int)n;
    }

    /// <summary>Writes bytes to the child's input (the PTY master).</summary>
    public void Write(byte[] data) => write(_master, data, data.Length);

    public bool HasExited
    {
        get
        {
            if (_pid == 0) return true;
            int r = waitpid(_pid, out _, WNOHANG);
            if (r == _pid) { _pid = 0; return true; }
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_pid != 0)
        {
            kill(_pid, SIGTERM);
            // brief reap
            for (int i = 0; i < 20 && waitpid(_pid, out _, WNOHANG) == 0; i++)
                Thread.Sleep(10);
            _pid = 0;
        }
        close(_master);
    }
}
