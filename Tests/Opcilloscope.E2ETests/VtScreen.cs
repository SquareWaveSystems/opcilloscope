using System.Text;

namespace Opcilloscope.E2ETests;

/// <summary>
/// A minimal VT100/ANSI screen emulator: feed it the bytes a Terminal.Gui app writes and it
/// reconstructs the rendered character grid for assertions. It deliberately understands only
/// what Terminal.Gui emits — cursor positioning, erases, and printable runs — and ignores
/// colour/style (SGR), mode toggles, and OSC strings. Maintained in-tree rather than depending
/// on the stale VtNetCore / XtermSharp packages.
/// </summary>
public sealed class VtScreen
{
    private readonly int _rows;
    private readonly int _cols;
    private readonly char[,] _grid;
    private int _row, _col;

    public VtScreen(int rows, int cols)
    {
        _rows = rows;
        _cols = cols;
        _grid = new char[rows, cols];
        Clear();
    }

    private void Clear()
    {
        for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _cols; c++)
                _grid[r, c] = ' ';
    }

    /// <summary>Feeds a chunk of already-UTF-8-decoded output, updating the grid and cursor.</summary>
    public void Feed(string s)
    {
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '\x1b') { i = HandleEscape(s, i); continue; }
            switch (c)
            {
                case '\r': _col = 0; break;
                case '\n': NewLine(); break;
                case '\b': _col = Math.Max(0, _col - 1); break;
                case '\t': _col = Math.Min(_cols - 1, (_col / 8 + 1) * 8); break;
                default:
                    if (c >= ' ')
                    {
                        if (_col >= _cols) { _col = 0; NewLine(); }
                        if (InBounds(_row, _col)) _grid[_row, _col] = c;
                        _col++;
                    }
                    break;
            }
            i++;
        }
    }

    private int HandleEscape(string s, int i)
    {
        // s[i] == ESC
        if (i + 1 >= s.Length) return i + 1;
        char n = s[i + 1];
        switch (n)
        {
            case '[': return HandleCsi(s, i + 2);
            case ']': return SkipOsc(s, i + 2);
            case 'P': case '^': case '_': return SkipUntilSt(s, i + 2); // DCS/PM/APC
            case '(': case ')': case '*': case '+': return i + 3;        // charset designation
            default: return i + 2;                                        // ESC = / ESC > / etc.
        }
    }

    private int HandleCsi(string s, int i)
    {
        int start = i;
        bool priv = i < s.Length && (s[i] == '?' || s[i] == '>' || s[i] == '!');
        if (priv) i++;
        while (i < s.Length && !(s[i] >= '@' && s[i] <= '~')) i++;
        if (i >= s.Length) return i;
        char final = s[i];
        string body = s.Substring(start + (priv ? 1 : 0), i - start - (priv ? 1 : 0));
        var p = ParseParams(body);

        if (!priv)
        {
            switch (final)
            {
                case 'H': case 'f': _row = Clamp(P(p, 0, 1) - 1, _rows); _col = Clamp(P(p, 1, 1) - 1, _cols); break;
                case 'A': _row = Clamp(_row - Math.Max(1, P(p, 0, 1)), _rows); break;
                case 'B': _row = Clamp(_row + Math.Max(1, P(p, 0, 1)), _rows); break;
                case 'C': _col = Clamp(_col + Math.Max(1, P(p, 0, 1)), _cols); break;
                case 'D': _col = Clamp(_col - Math.Max(1, P(p, 0, 1)), _cols); break;
                case 'E': _col = 0; _row = Clamp(_row + Math.Max(1, P(p, 0, 1)), _rows); break;
                case 'F': _col = 0; _row = Clamp(_row - Math.Max(1, P(p, 0, 1)), _rows); break;
                case 'G': _col = Clamp(P(p, 0, 1) - 1, _cols); break;
                case 'd': _row = Clamp(P(p, 0, 1) - 1, _rows); break;
                case 'J': EraseDisplay(P(p, 0, 0)); break;
                case 'K': EraseLine(P(p, 0, 0)); break;
                // 'm' (SGR), 'h'/'l' (modes), 'r', 'n', 't', etc. → ignored
            }
        }
        return i + 1;
    }

    private void EraseDisplay(int mode)
    {
        if (mode == 2 || mode == 3) { Clear(); return; }
        if (mode == 0) { EraseLine(0); for (int r = _row + 1; r < _rows; r++) for (int c = 0; c < _cols; c++) _grid[r, c] = ' '; }
        else if (mode == 1) { for (int r = 0; r < _row; r++) for (int c = 0; c < _cols; c++) _grid[r, c] = ' '; EraseLine(1); }
    }

    private void EraseLine(int mode)
    {
        if (!InBounds(_row, 0)) return;
        int from = mode == 0 ? _col : 0;
        int to = mode == 1 ? _col : _cols - 1;
        for (int c = Math.Max(0, from); c <= Math.Min(_cols - 1, to); c++) _grid[_row, c] = ' ';
    }

    private void NewLine()
    {
        if (_row < _rows - 1) _row++;
        // (no scrollback needed: Terminal.Gui repaints absolutely)
    }

    private static int SkipOsc(string s, int i)
    {
        while (i < s.Length)
        {
            if (s[i] == '\x07') return i + 1;                    // BEL terminator
            if (s[i] == '\x1b' && i + 1 < s.Length && s[i + 1] == '\\') return i + 2; // ST
            i++;
        }
        return i;
    }

    private static int SkipUntilSt(string s, int i)
    {
        while (i < s.Length)
        {
            if (s[i] == '\x1b' && i + 1 < s.Length && s[i + 1] == '\\') return i + 2;
            i++;
        }
        return i;
    }

    private static List<int> ParseParams(string body)
    {
        var list = new List<int>();
        foreach (var part in body.Split(';'))
            list.Add(int.TryParse(part, out int v) ? v : 0);
        return list;
    }

    private static int P(List<int> p, int idx, int def) => idx < p.Count && p[idx] != 0 ? p[idx] : (idx < p.Count ? p[idx] : def);
    private int Clamp(int v, int max) => Math.Max(0, Math.Min(max - 1, v));
    private bool InBounds(int r, int c) => r >= 0 && r < _rows && c >= 0 && c < _cols;

    /// <summary>The full screen as text, one line per row (trailing spaces trimmed per row).</summary>
    public string Text()
    {
        var sb = new StringBuilder();
        for (int r = 0; r < _rows; r++)
        {
            var line = new StringBuilder();
            for (int c = 0; c < _cols; c++) line.Append(_grid[r, c]);
            sb.Append(line.ToString().TrimEnd());
            if (r < _rows - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    public bool Contains(string needle) => Text().Contains(needle);
}
