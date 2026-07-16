using System.Diagnostics;
using System.Text;

namespace Opcilloscope.E2ETests;

/// <summary>
/// Runs opcilloscope over a real PTY and reconstructs its rendered terminal screen.
/// </summary>
public sealed class OpcilloscopeSession : IDisposable
{
    private readonly Pty _pty;
    private readonly VtScreen _screen;
    private readonly Thread _reader;
    private readonly object _gate = new();
    private volatile bool _stop;
    private string _queryPending = string.Empty;
    private volatile Exception? _readerFailure;

    public OpcilloscopeSession(
        string binaryPath,
        IReadOnlyList<string>? arguments = null,
        int rows = 30,
        int cols = 100,
        IReadOnlyDictionary<string, string>? extraEnvironment = null)
    {
        Rows = rows;
        Cols = cols;
        _screen = new VtScreen(rows, cols);
        _pty = Pty.Spawn(
            binaryPath,
            arguments ?? Array.Empty<string>(),
            rows,
            cols,
            extraEnvironment);
        _reader = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name = "opcilloscope-e2e-pty-reader",
        };
        _reader.Start();
    }

    public int Rows { get; }

    public int Cols { get; }

    public bool HasExited => _pty.HasExited;

    public int? ExitCode => _pty.ExitCode;

    public string Snapshot()
    {
        lock (_gate)
        {
            return _screen.Text();
        }
    }

    public bool WaitForText(string text, TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            ThrowIfReaderFailed();
            if (Snapshot().Contains(text, StringComparison.Ordinal))
            {
                return true;
            }

            if (_pty.HasExited)
            {
                break;
            }

            Thread.Sleep(40);
        }

        ThrowIfReaderFailed();
        return Snapshot().Contains(text, StringComparison.Ordinal);
    }

    public bool WaitForExit(TimeSpan timeout) => _pty.WaitForExit(timeout);

    public void Send(string input) => _pty.Write(Encoding.UTF8.GetBytes(input));

    public void SendByte(byte input) => _pty.Write([input]);

    public void Dispose()
    {
        _stop = true;
        _pty.Dispose();
        _reader.Join(TimeSpan.FromSeconds(2));
    }

    private void ReadLoop()
    {
        try
        {
            var decoder = Encoding.UTF8.GetDecoder();
            var bytes = new byte[8192];
            var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];

            while (!_stop)
            {
                var byteCount = _pty.Read(bytes);
                if (byteCount <= 0)
                {
                    break;
                }

                var charCount = decoder.GetChars(bytes, 0, byteCount, chars, 0);
                var chunk = new string(chars, 0, charCount);
                lock (_gate)
                {
                    _screen.Feed(chunk);
                    RespondToQueries(chunk);
                }
            }
        }
        catch (Exception exception) when (_stop && exception is ObjectDisposedException or IOException)
        {
            // Expected when Dispose closes the PTY to release a blocking read.
        }
        catch (Exception exception)
        {
            _readerFailure = exception;
        }
    }

    private void RespondToQueries(string chunk)
    {
        _queryPending += chunk;

        while (TakeQuery("\x1b[18t"))
        {
            Reply($"\x1b[8;{Rows};{Cols}t");
        }

        while (TakeQuery("\x1b[6n"))
        {
            Reply("\x1b[1;1R");
        }

        while (TakeQuery("\x1b]10;?"))
        {
            Reply("\x1b]10;rgb:cccc/cccc/cccc\x1b\\");
        }

        while (TakeQuery("\x1b]11;?"))
        {
            Reply("\x1b]11;rgb:0000/0000/0000\x1b\\");
        }

        if (_queryPending.Length > 4096)
        {
            _queryPending = _queryPending[^1024..];
        }
    }

    private bool TakeQuery(string marker)
    {
        var index = _queryPending.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
        {
            return false;
        }

        _queryPending = _queryPending.Remove(index, marker.Length);
        return true;
    }

    private void Reply(string value) => _pty.Write(Encoding.ASCII.GetBytes(value));

    private void ThrowIfReaderFailed()
    {
        if (_readerFailure is { } failure)
        {
            throw new InvalidOperationException("The PTY reader failed.", failure);
        }
    }
}
