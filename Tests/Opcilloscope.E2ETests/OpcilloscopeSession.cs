using System.Diagnostics;
using System.Text;

namespace Opcilloscope.E2ETests;

/// <summary>
/// Drives the published opcilloscope binary over a <see cref="Pty"/>: pumps its output into a
/// <see cref="VtScreen"/>, answers the terminal capability queries Terminal.Gui's net driver
/// sends (so it actually paints), and exposes the reconstructed screen plus key input.
/// </summary>
public sealed class OpcilloscopeSession : IDisposable
{
    private readonly Pty _pty;
    private readonly VtScreen _screen;
    private readonly Thread _reader;
    private readonly object _gate = new();
    private volatile bool _stop;
    private string _pending = "";

    public int Rows { get; }
    public int Cols { get; }

    public OpcilloscopeSession(string binaryPath, IReadOnlyList<string>? args = null, int rows = 30, int cols = 100)
    {
        Rows = rows;
        Cols = cols;
        _screen = new VtScreen(rows, cols);
        _pty = Pty.Spawn(binaryPath, args ?? Array.Empty<string>(), rows, cols);
        _reader = new Thread(ReadLoop) { IsBackground = true, Name = "pty-reader" };
        _reader.Start();
    }

    private void ReadLoop()
    {
        var decoder = Encoding.UTF8.GetDecoder();
        var buf = new byte[8192];
        var chars = new char[8192];
        while (!_stop)
        {
            int n = _pty.Read(buf);
            if (n <= 0) break; // EOF / process gone
            int cn = decoder.GetChars(buf, 0, n, chars, 0);
            var chunk = new string(chars, 0, cn);
            lock (_gate)
            {
                _screen.Feed(chunk);
                RespondToQueries(chunk);
            }
        }
    }

    /// <summary>
    /// Terminal.Gui's net driver detects size/capabilities by emitting queries and waiting for
    /// the terminal's reply. A bare PTY has no emulator on the other end, so we answer the ones
    /// that gate the first paint: text-area size (CSI 18 t), cursor position (DSR 6 n), and the
    /// OSC 10/11 fg/bg colour queries.
    /// </summary>
    private void RespondToQueries(string chunk)
    {
        _pending += chunk;
        if (_pending.Length > 2048) _pending = _pending[^512..];

        if (Take("\x1b[18t")) Reply($"\x1b[8;{Rows};{Cols}t");
        if (Take("\x1b[6n")) Reply($"\x1b[{Rows};{Cols}R");
        if (Take("\x1b]10;?")) Reply("\x1b]10;rgb:cccc/cccc/cccc\x1b\\");
        if (Take("\x1b]11;?")) Reply("\x1b]11;rgb:0000/0000/0000\x1b\\");
    }

    private bool Take(string marker)
    {
        int idx = _pending.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return false;
        _pending = _pending.Remove(idx, marker.Length);
        return true;
    }

    private void Reply(string s) => _pty.Write(Encoding.ASCII.GetBytes(s));

    /// <summary>Current reconstructed screen as text (one line per row).</summary>
    public string Snapshot()
    {
        lock (_gate) return _screen.Text();
    }

    /// <summary>Blocks until the screen contains <paramref name="text"/> or the timeout elapses.</summary>
    public bool WaitForText(string text, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (Snapshot().Contains(text)) return true;
            if (_pty.HasExited) return Snapshot().Contains(text);
            Thread.Sleep(40);
        }
        return Snapshot().Contains(text);
    }

    /// <summary>Sends raw bytes as terminal input.</summary>
    public void Send(string raw) => _pty.Write(Encoding.UTF8.GetBytes(raw));

    /// <summary>Sends a single byte (e.g. a control character).</summary>
    public void SendByte(byte b) => _pty.Write(new[] { b });

    public void Dispose()
    {
        _stop = true;
        _pty.Dispose();
        _reader.Join(500);
    }
}
