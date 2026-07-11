using System.Text;

namespace Opcilloscope.E2ETests;

/// <summary>
/// Minimal VT100/ANSI screen used to reconstruct Terminal.Gui output for assertions.
/// </summary>
public sealed class VtScreen
{
    private readonly int _rows;
    private readonly int _cols;
    private readonly char[,] _grid;
    private int _row;
    private int _col;
    private string _pending = string.Empty;

    public VtScreen(int rows, int cols)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rows, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(cols, 1);

        _rows = rows;
        _cols = cols;
        _grid = new char[rows, cols];
        Clear();
    }

    public void Feed(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        input = _pending + input;
        _pending = string.Empty;

        var i = 0;
        while (i < input.Length)
        {
            var c = input[i];
            if (c == '\x1b')
            {
                if (!TryHandleEscape(input, i, out var nextIndex))
                {
                    _pending = input[i..];
                    break;
                }

                i = nextIndex;
                continue;
            }

            switch (c)
            {
                case '\r':
                    _col = 0;
                    break;
                case '\n':
                    NewLine();
                    break;
                case '\b':
                    _col = Math.Max(0, _col - 1);
                    break;
                case '\t':
                    _col = Math.Min(_cols - 1, ((_col / 8) + 1) * 8);
                    break;
                default:
                    if (c >= ' ')
                    {
                        if (_col >= _cols)
                        {
                            _col = 0;
                            NewLine();
                        }

                        if (InBounds(_row, _col))
                        {
                            _grid[_row, _col] = c;
                        }

                        _col++;
                    }
                    break;
            }

            i++;
        }
    }

    public string Text()
    {
        var output = new StringBuilder();
        for (var row = 0; row < _rows; row++)
        {
            var line = new StringBuilder(_cols);
            for (var col = 0; col < _cols; col++)
            {
                line.Append(_grid[row, col]);
            }

            output.Append(line.ToString().TrimEnd());
            if (row < _rows - 1)
            {
                output.Append('\n');
            }
        }

        return output.ToString();
    }

    private bool TryHandleEscape(string input, int index, out int nextIndex)
    {
        if (index + 1 >= input.Length)
        {
            nextIndex = index;
            return false;
        }

        switch (input[index + 1])
        {
            case '[':
                return TryHandleCsi(input, index + 2, out nextIndex);
            case ']':
                return TrySkipOsc(input, index + 2, out nextIndex);
            case 'P':
            case '^':
            case '_':
                return TrySkipUntilStringTerminator(input, index + 2, out nextIndex);
            case '(':
            case ')':
            case '*':
            case '+':
                if (index + 2 >= input.Length)
                {
                    nextIndex = index;
                    return false;
                }

                nextIndex = index + 3;
                return true;
            default:
                nextIndex = index + 2;
                return true;
        }
    }

    private bool TryHandleCsi(string input, int index, out int nextIndex)
    {
        var start = index;
        var isPrivate = index < input.Length && input[index] is '?' or '>' or '!';
        if (isPrivate)
        {
            index++;
        }

        while (index < input.Length && input[index] is not (>= '@' and <= '~'))
        {
            index++;
        }

        if (index >= input.Length)
        {
            nextIndex = start - 2;
            return false;
        }

        var final = input[index];
        var bodyStart = start + (isPrivate ? 1 : 0);
        var body = input.Substring(bodyStart, index - bodyStart);
        var parameters = ParseParameters(body);

        if (!isPrivate)
        {
            switch (final)
            {
                case 'H':
                case 'f':
                    _row = Clamp(Parameter(parameters, 0, 1) - 1, _rows);
                    _col = Clamp(Parameter(parameters, 1, 1) - 1, _cols);
                    break;
                case 'A':
                    _row = Clamp(_row - Math.Max(1, Parameter(parameters, 0, 1)), _rows);
                    break;
                case 'B':
                    _row = Clamp(_row + Math.Max(1, Parameter(parameters, 0, 1)), _rows);
                    break;
                case 'C':
                    _col = Clamp(_col + Math.Max(1, Parameter(parameters, 0, 1)), _cols);
                    break;
                case 'D':
                    _col = Clamp(_col - Math.Max(1, Parameter(parameters, 0, 1)), _cols);
                    break;
                case 'E':
                    _col = 0;
                    _row = Clamp(_row + Math.Max(1, Parameter(parameters, 0, 1)), _rows);
                    break;
                case 'F':
                    _col = 0;
                    _row = Clamp(_row - Math.Max(1, Parameter(parameters, 0, 1)), _rows);
                    break;
                case 'G':
                    _col = Clamp(Parameter(parameters, 0, 1) - 1, _cols);
                    break;
                case 'd':
                    _row = Clamp(Parameter(parameters, 0, 1) - 1, _rows);
                    break;
                case 'J':
                    EraseDisplay(Parameter(parameters, 0, 0));
                    break;
                case 'K':
                    EraseLine(Parameter(parameters, 0, 0));
                    break;
            }
        }

        nextIndex = index + 1;
        return true;
    }

    private void EraseDisplay(int mode)
    {
        if (mode is 2 or 3)
        {
            Clear();
            return;
        }

        if (mode == 0)
        {
            EraseLine(0);
            for (var row = _row + 1; row < _rows; row++)
            {
                for (var col = 0; col < _cols; col++)
                {
                    _grid[row, col] = ' ';
                }
            }
        }
        else if (mode == 1)
        {
            for (var row = 0; row < _row; row++)
            {
                for (var col = 0; col < _cols; col++)
                {
                    _grid[row, col] = ' ';
                }
            }

            EraseLine(1);
        }
    }

    private void EraseLine(int mode)
    {
        if (!InBounds(_row, 0))
        {
            return;
        }

        var from = mode == 0 ? _col : 0;
        var to = mode == 1 ? _col : _cols - 1;
        for (var col = Math.Max(0, from); col <= Math.Min(_cols - 1, to); col++)
        {
            _grid[_row, col] = ' ';
        }
    }

    private void Clear()
    {
        for (var row = 0; row < _rows; row++)
        {
            for (var col = 0; col < _cols; col++)
            {
                _grid[row, col] = ' ';
            }
        }
    }

    private void NewLine()
    {
        if (_row < _rows - 1)
        {
            _row++;
        }
    }

    private static bool TrySkipOsc(string input, int index, out int nextIndex)
    {
        while (index < input.Length)
        {
            if (input[index] == '\x07')
            {
                nextIndex = index + 1;
                return true;
            }

            if (input[index] == '\x1b' && index + 1 < input.Length && input[index + 1] == '\\')
            {
                nextIndex = index + 2;
                return true;
            }

            index++;
        }

        nextIndex = index;
        return false;
    }

    private static bool TrySkipUntilStringTerminator(string input, int index, out int nextIndex)
    {
        while (index < input.Length)
        {
            if (input[index] == '\x1b' && index + 1 < input.Length && input[index + 1] == '\\')
            {
                nextIndex = index + 2;
                return true;
            }

            index++;
        }

        nextIndex = index;
        return false;
    }

    private static List<int> ParseParameters(string body) =>
        body.Split(';')
            .Select(part => int.TryParse(part, out var value) ? value : 0)
            .ToList();

    private static int Parameter(IReadOnlyList<int> parameters, int index, int defaultValue) =>
        index < parameters.Count && parameters[index] > 0
            ? parameters[index]
            : defaultValue;

    private static int Clamp(int value, int maximum) => Math.Max(0, Math.Min(maximum - 1, value));

    private bool InBounds(int row, int col) => row >= 0 && row < _rows && col >= 0 && col < _cols;
}
