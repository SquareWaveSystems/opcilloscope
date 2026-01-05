using Terminal.Gui;
using OpcScope.OpcUa;
using OpcScope.OpcUa.Models;
using OpcScope.App.Themes;
using OpcScope.Utilities;
using System.Text;
using Attribute = Terminal.Gui.Attribute;
using Rune = System.Text.Rune;

namespace OpcScope.App.Views;

/// <summary>
/// Real-time 2D scrolling oscilloscope-style plot with retro-futuristic CRT aesthetic.
/// Inspired by 1970s-80s industrial control displays and cassette futurism.
/// Supports multiple themes via ThemeManager.
/// </summary>
public class TrendPlotView : View
{
    // === Theme-aware color accessors ===
    private static RetroTheme Theme => ThemeManager.Current;
    private static Attribute BrightAttr => Theme.BrightAttr;
    private static Attribute NormalAttr => Theme.NormalAttr;
    private static Attribute DimAttr => Theme.DimAttr;
    private static Attribute GridAttr => Theme.GridAttr;
    private static Attribute BorderAttr => Theme.BorderAttr;
    private static Attribute StatusActiveAttr => Theme.StatusActiveAttr;
    private static Attribute StatusInactiveAttr => Theme.StatusInactiveAttr;
    private static Attribute ScanlineAttr => Theme.ScanlineAttr;
    private static Attribute GlowAttr => Theme.GlowAttr;
    private static Attribute BackgroundAttr => new(Theme.Background, Theme.Background);

    // Ring buffer for samples (preallocated)
    private readonly float[] _samples;
    private int _writeIndex;
    private int _sampleCount;
    private readonly object _lock = new();

    // Auto-scale tracking
    private float _visibleMin = float.MaxValue;
    private float _visibleMax = float.MinValue;
    private bool _autoScale = true;
    private float _scaleMultiplier = 1.0f;

    // State
    private bool _isPaused;
    private MonitoredNode? _boundNode;
    private SubscriptionManager? _subscriptionManager;
    private Action<MonitoredNode>? _valueChangedHandler;
    private object? _timerToken;
    private float _demoPhase;
    private int _frameCount;

    // Layout constants
    private const int LeftMargin = 10;
    private const int TopMargin = 3;
    private const int BottomMargin = 3;
    private const int RightMargin = 2;

    /// <summary>
    /// Event fired when pause state changes.
    /// </summary>
    public event Action<bool>? PauseStateChanged;

    /// <summary>
    /// Gets whether the plot is currently paused.
    /// </summary>
    public bool IsPaused => _isPaused;

    /// <summary>
    /// Gets the currently bound node, if any.
    /// </summary>
    public MonitoredNode? BoundNode => _boundNode;

    public TrendPlotView()
    {
        // Pre-allocate ring buffer for maximum expected width (200 samples)
        _samples = new float[200];

        CanFocus = true;
        WantMousePositionReports = false;
    }

    /// <summary>
    /// Adds a sample to the ring buffer. Thread-safe.
    /// </summary>
    private void AddSample(float value)
    {
        if (_isPaused) return;

        lock (_lock)
        {
            _samples[_writeIndex] = value;
            _writeIndex = (_writeIndex + 1) % _samples.Length;
            if (_sampleCount < _samples.Length)
                _sampleCount++;
        }
    }

    /// <summary>
    /// Binds to a monitored node to auto-subscribe to value changes.
    /// </summary>
    public void BindToMonitoredNode(MonitoredNode node, SubscriptionManager subscriptionManager)
    {
        // Unbind previous
        UnbindCurrentNode();

        _boundNode = node;
        _subscriptionManager = subscriptionManager;

        // Subscribe to value changes from subscription manager
        _valueChangedHandler = OnBoundNodeValueChanged;
        _subscriptionManager.ValueChanged += _valueChangedHandler;

        // Try to parse current value as initial sample
        if (TryParseValue(node.Value, out var value))
        {
            AddSample(value);
        }
        else
        {
            // Provide feedback when the bound node's value cannot be interpreted as numeric
            var displayName = string.IsNullOrWhiteSpace(node.DisplayName)
                ? "Selected node"
                : node.DisplayName;

            UiThread.Run(() =>
            {
                MessageBox.ErrorQuery(
                    "Unsupported value type",
                    $"{displayName} has a non-numeric value and cannot be plotted.",
                    "OK");
            });
        }

        // Start the refresh timer to redraw the plot as new samples arrive
        StartUpdateTimer();

        SetNeedsLayout();
    }

    private void OnBoundNodeValueChanged(MonitoredNode node)
    {
        MonitoredNode? currentBoundNode;
        lock (_lock)
        {
            currentBoundNode = _boundNode;
        }

        if (currentBoundNode != null &&
            node.ClientHandle == currentBoundNode.ClientHandle &&
            TryParseValue(node.Value, out var value))
        {
            AddSample(value);
        }
    }

    private void UnbindCurrentNode()
    {
        StopUpdateTimer();
        if (_valueChangedHandler != null && _subscriptionManager != null)
        {
            _subscriptionManager.ValueChanged -= _valueChangedHandler;
        }
        _valueChangedHandler = null;
        _subscriptionManager = null;
        _boundNode = null;
    }

    /// <summary>
    /// Clears all samples and resets the view.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_samples);
            _writeIndex = 0;
            _sampleCount = 0;
            _visibleMin = float.MaxValue;
            _visibleMax = float.MinValue;
        }
        SetNeedsLayout();
    }

    /// <summary>
    /// Starts the demo sine wave animation.
    /// </summary>
    public void StartDemoMode()
    {
        UnbindCurrentNode();
        _boundNode = new MonitoredNode { DisplayName = "SineWave" };
        _demoPhase = 0;
        StartUpdateTimer();
    }

    /// <summary>
    /// Stops the demo mode.
    /// </summary>
    public void StopDemoMode()
    {
        StopUpdateTimer();
    }

    private void StartUpdateTimer()
    {
        if (_timerToken != null) return;

        // ~10 FPS update rate
        _timerToken = Application.AddTimeout(TimeSpan.FromMilliseconds(100), OnTimerTick);
    }

    private void StopUpdateTimer()
    {
        if (_timerToken != null)
        {
            Application.RemoveTimeout(_timerToken);
            _timerToken = null;
        }
    }

    private bool OnTimerTick()
    {
        _frameCount++;

        string? displayName;
        lock (_lock)
        {
            displayName = _boundNode?.DisplayName;
        }

        if (!_isPaused && displayName == "SineWave")
        {
            // Generate demo sine wave
            _demoPhase += 0.15f;
            var value = (float)(50 + 40 * Math.Sin(_demoPhase));
            AddSample(value);
        }

        SetNeedsLayout();
        return true; // Keep timer running
    }

    /// <summary>
    /// Toggles pause state.
    /// </summary>
    public void TogglePause()
    {
        _isPaused = !_isPaused;
        PauseStateChanged?.Invoke(_isPaused);
        SetNeedsLayout();
    }

    /// <summary>
    /// Increases vertical scale (zoom in).
    /// </summary>
    public void IncreaseScale()
    {
        _autoScale = false;
        _scaleMultiplier *= 1.2f;
        SetNeedsLayout();
    }

    /// <summary>
    /// Decreases vertical scale (zoom out).
    /// </summary>
    public void DecreaseScale()
    {
        _autoScale = false;
        _scaleMultiplier /= 1.2f;
        if (_scaleMultiplier < 0.1f) _scaleMultiplier = 0.1f;
        SetNeedsLayout();
    }

    /// <summary>
    /// Resets to auto-scale mode.
    /// </summary>
    public void ResetScale()
    {
        _autoScale = true;
        _scaleMultiplier = 1.0f;
        SetNeedsLayout();
    }

    protected override bool OnKeyDown(Key key)
    {
        switch (key.KeyCode)
        {
            case KeyCode.Space:
                TogglePause();
                return true;

            case KeyCode.D0 when key.IsShift: // + key (Shift+0 on some keyboards)
            case (KeyCode)'=': // + without shift
            case (KeyCode)'+':
                IncreaseScale();
                return true;

            case KeyCode.D9 when key.IsShift: // ( key - sometimes used
            case (KeyCode)'-':
                DecreaseScale();
                return true;

            case (KeyCode)'r':
            case (KeyCode)'R':
                ResetScale();
                return true;
        }

        return base.OnKeyDown(key);
    }

    protected override bool OnDrawingContent(DrawContext context)
    {
        base.OnDrawingContent(context);

        var viewport = Frame;

        // Calculate plot area
        int plotWidth = viewport.Width - LeftMargin - RightMargin;
        int plotHeight = viewport.Height - TopMargin - BottomMargin;

        if (plotWidth < 4 || plotHeight < 2) return true;

        // Clear with theme background
        Driver.SetAttribute(BackgroundAttr);
        for (int y = 0; y < viewport.Height; y++)
        {
            Move(0, y);
            AddStr(new string(' ', viewport.Width));
        }

        // Get samples to display
        float[] displaySamples;
        int displayCount;
        lock (_lock)
        {
            displayCount = Math.Min(_sampleCount, plotWidth);
            displaySamples = new float[displayCount];

            if (displayCount > 0)
            {
                int startIdx = (_writeIndex - displayCount + _samples.Length) % _samples.Length;
                for (int i = 0; i < displayCount; i++)
                {
                    displaySamples[i] = _samples[(startIdx + i) % _samples.Length];
                }
            }
        }

        // Calculate min/max for scaling
        float minVal = float.MaxValue;
        float maxVal = float.MinValue;

        if (displayCount > 0)
        {
            for (int i = 0; i < displayCount; i++)
            {
                if (displaySamples[i] < minVal) minVal = displaySamples[i];
                if (displaySamples[i] > maxVal) maxVal = displaySamples[i];
            }
        }
        else
        {
            minVal = 0;
            maxVal = 100;
        }

        // Apply auto-scale or manual scale
        if (_autoScale)
        {
            _visibleMin = minVal;
            _visibleMax = maxVal;
        }
        else
        {
            float center = (minVal + maxVal) / 2;
            float range = (maxVal - minVal) / 2;
            range = Math.Max(range, 1f) / _scaleMultiplier;
            _visibleMin = center - range;
            _visibleMax = center + range;
        }

        // Ensure we have a valid range
        if (_visibleMax <= _visibleMin)
        {
            _visibleMin = minVal - 1;
            _visibleMax = maxVal + 1;
        }

        // Add padding
        float padding = (_visibleMax - _visibleMin) * 0.1f;
        if (padding < 0.1f) padding = 0.1f;
        _visibleMin -= padding;
        _visibleMax += padding;

        // Draw in order: grid, border, data, status
        DrawScanlines(viewport.Width, viewport.Height);
        DrawHeader(viewport.Width);
        DrawIndustrialFrame(viewport.Width, viewport.Height, plotWidth, plotHeight);
        DrawGrid(plotWidth, plotHeight);
        DrawYAxisLabels(plotHeight);

        if (displayCount > 0)
        {
            DrawWaveform(displaySamples, displayCount, plotWidth, plotHeight);
        }
        else
        {
            DrawNoSignal(plotWidth, plotHeight);
        }

        DrawStatusBar(viewport.Width, viewport.Height);

        return true;
    }

    private void DrawScanlines(int width, int height)
    {
        // Subtle scanline effect on alternating rows (if theme enables it)
        if (!Theme.EnableScanlines) return;

        Driver.SetAttribute(ScanlineAttr);
        for (int y = 1; y < height; y += 2)
        {
            Move(0, y);
            for (int x = 0; x < width; x++)
            {
                AddRune((Rune)'░');
            }
        }
    }

    private void DrawHeader(int width)
    {
        // Industrial header bar
        Driver.SetAttribute(BorderAttr);
        Move(0, 0);
        AddRune((Rune)Theme.BoxTopLeft);
        for (int x = 1; x < width - 1; x++) AddRune((Rune)Theme.BoxHorizontal);
        AddRune((Rune)Theme.BoxTopRight);

        // Title with signal name
        string title = _boundNode?.DisplayName?.ToUpperInvariant() ?? "SCOPE";
        if (title.Length > 20) title = title[..20];

        Move(2, 0);
        Driver.SetAttribute(BrightAttr);
        AddStr($"{Theme.BoxTitleLeft} {title} {Theme.BoxTitleRight}");

        // Status indicators on right
        int rightPos = width - 25;
        if (rightPos > title.Length + 10)
        {
            Move(rightPos, 0);
            Driver.SetAttribute(BorderAttr);
            AddStr($"{Theme.BoxTitleLeft}");

            // LIVE/HOLD indicator
            if (_isPaused)
            {
                Driver.SetAttribute(StatusInactiveAttr);
                AddStr(" HOLD ");
            }
            else
            {
                Driver.SetAttribute(StatusActiveAttr);
                AddStr(" LIVE ");
            }

            Driver.SetAttribute(BorderAttr);
            AddStr("│");

            // Blinking activity indicator
            if (!_isPaused && (_frameCount % 10) < 5)
            {
                Driver.SetAttribute(StatusActiveAttr);
                AddStr("●");
            }
            else
            {
                Driver.SetAttribute(StatusInactiveAttr);
                AddStr("○");
            }

            Driver.SetAttribute(BorderAttr);
            AddStr($"{Theme.BoxTitleRight}");
        }

        // Second header line with technical info
        Move(0, 1);
        Driver.SetAttribute(BorderAttr);
        AddRune((Rune)Theme.BoxVertical);

        Driver.SetAttribute(DimAttr);
        string techInfo = $" CH1  SCALE:{(_autoScale ? "AUTO" : $"{_scaleMultiplier:F1}X")}  ";
        lock (_lock)
        {
            techInfo += $"SAMPLES:{_sampleCount,4}  ";
        }
        techInfo += $"RANGE:[{FormatAxisValue(_visibleMin)},{FormatAxisValue(_visibleMax)}]";

        AddStr(techInfo.PadRight(width - 2));

        Move(width - 1, 1);
        Driver.SetAttribute(BorderAttr);
        AddRune((Rune)Theme.BoxVertical);
    }

    private void DrawIndustrialFrame(int width, int height, int plotWidth, int plotHeight)
    {
        int plotTop = TopMargin - 1;
        int plotBottom = TopMargin + plotHeight;
        int plotLeft = LeftMargin - 1;
        int plotRight = LeftMargin + plotWidth;

        Driver.SetAttribute(BorderAttr);

        // Top border of plot area
        Move(plotLeft, plotTop);
        AddRune((Rune)Theme.BoxTopLeft);
        for (int x = plotLeft + 1; x < plotRight; x++)
        {
            // Tick marks every 10 columns
            if ((x - LeftMargin) % 10 == 0 && x < plotRight - 1)
                AddRune((Rune)Theme.TickHorizontal);
            else
                AddRune((Rune)Theme.BoxHorizontal);
        }
        AddRune((Rune)Theme.BoxTopRight);

        // Side borders
        for (int y = plotTop + 1; y < plotBottom; y++)
        {
            Move(plotLeft, y);
            // Tick marks every 4 rows
            if ((y - TopMargin) % 4 == 0)
                AddRune((Rune)Theme.TickVertical);
            else
                AddRune((Rune)Theme.BoxVertical);

            Move(plotRight, y);
            if ((y - TopMargin) % 4 == 0)
                AddRune((Rune)'╢');
            else
                AddRune((Rune)Theme.BoxVertical);
        }

        // Bottom border
        Move(plotLeft, plotBottom);
        AddRune((Rune)Theme.BoxBottomLeft);
        for (int x = plotLeft + 1; x < plotRight; x++)
        {
            if ((x - LeftMargin) % 10 == 0 && x < plotRight - 1)
                AddRune((Rune)'╧');
            else
                AddRune((Rune)Theme.BoxHorizontal);
        }
        AddRune((Rune)Theme.BoxBottomRight);

        // Corner ornaments
        Driver.SetAttribute(DimAttr);
        Move(plotLeft - 1, plotTop);
        AddStr("▐");
        Move(plotRight + 1, plotTop);
        AddStr("▌");
        Move(plotLeft - 1, plotBottom);
        AddStr("▐");
        Move(plotRight + 1, plotBottom);
        AddStr("▌");
    }

    private void DrawGrid(int plotWidth, int plotHeight)
    {
        Driver.SetAttribute(GridAttr);

        // Horizontal grid lines
        for (int y = 0; y < plotHeight; y++)
        {
            if (y % 4 == 0 && y > 0)
            {
                for (int x = 0; x < plotWidth; x++)
                {
                    // Dashed line pattern
                    if (x % 3 < 2)
                    {
                        Move(LeftMargin + x, TopMargin + y);
                        AddRune((Rune)'·');
                    }
                }
            }
        }

        // Vertical grid lines (lighter)
        for (int x = 0; x < plotWidth; x++)
        {
            if ((x % 10) == 0 && x > 0)
            {
                for (int y = 0; y < plotHeight; y++)
                {
                    if (y % 4 != 0) // Don't overwrite horizontal lines
                    {
                        Move(LeftMargin + x, TopMargin + y);
                        if (y % 2 == 0)
                            AddRune((Rune)'·');
                    }
                }
            }
        }

        // Center line (zero reference if applicable)
        float zeroY = 1.0f - (0 - _visibleMin) / (_visibleMax - _visibleMin);
        if (zeroY > 0 && zeroY < 1)
        {
            int centerRow = (int)(zeroY * (plotHeight - 1));
            Driver.SetAttribute(DimAttr);
            for (int x = 0; x < plotWidth; x++)
            {
                Move(LeftMargin + x, TopMargin + centerRow);
                AddRune((Rune)'─');
            }
        }
    }

    private void DrawYAxisLabels(int plotHeight)
    {
        Driver.SetAttribute(NormalAttr);

        // Max value
        string maxStr = FormatAxisValue(_visibleMax);
        Move(1, TopMargin);
        AddStr(maxStr.PadLeft(LeftMargin - 2));

        // Min value
        string minStr = FormatAxisValue(_visibleMin);
        Move(1, TopMargin + plotHeight - 1);
        AddStr(minStr.PadLeft(LeftMargin - 2));

        // Mid values
        if (plotHeight > 8)
        {
            float midVal = (_visibleMin + _visibleMax) / 2;
            string midStr = FormatAxisValue(midVal);
            Move(1, TopMargin + plotHeight / 2);
            AddStr(midStr.PadLeft(LeftMargin - 2));
        }

        if (plotHeight > 12)
        {
            float q1 = _visibleMin + (_visibleMax - _visibleMin) * 0.25f;
            float q3 = _visibleMin + (_visibleMax - _visibleMin) * 0.75f;

            Driver.SetAttribute(DimAttr);
            Move(1, TopMargin + plotHeight * 3 / 4);
            AddStr(FormatAxisValue(q1).PadLeft(LeftMargin - 2));
            Move(1, TopMargin + plotHeight / 4);
            AddStr(FormatAxisValue(q3).PadLeft(LeftMargin - 2));
        }
    }

    private void DrawWaveform(float[] samples, int sampleCount, int plotWidth, int plotHeight)
    {
        float range = _visibleMax - _visibleMin;
        if (range <= 0) range = 1;

        // Calculate Y positions for each sample
        int[] yPositions = new int[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float normalized = (samples[i] - _visibleMin) / range;
            normalized = Math.Clamp(normalized, 0, 1);
            yPositions[i] = TopMargin + (int)((1 - normalized) * (plotHeight - 1));
        }

        // Draw connecting lines first (dimmer)
        Driver.SetAttribute(DimAttr);
        for (int i = 0; i < sampleCount - 1; i++)
        {
            int x = LeftMargin + (plotWidth - sampleCount) + i;
            if (x < LeftMargin) continue;

            int y1 = yPositions[i];
            int y2 = yPositions[i + 1];

            // Draw vertical connection between points
            int minY = Math.Min(y1, y2);
            int maxY = Math.Max(y1, y2);

            for (int y = minY + 1; y < maxY; y++)
            {
                Move(x, y);
                AddRune((Rune)'│');
            }
        }

        // Draw the trace points (brighter)
        Driver.SetAttribute(BrightAttr);
        for (int i = 0; i < sampleCount; i++)
        {
            int x = LeftMargin + (plotWidth - sampleCount) + i;
            if (x < LeftMargin) continue;

            int y = yPositions[i];
            Move(x, y);

            // Use different characters based on slope
            if (i > 0 && i < sampleCount - 1)
            {
                int prevY = yPositions[i - 1];
                int nextY = yPositions[i + 1];

                if (prevY > y && nextY > y)
                    AddRune((Rune)'▀'); // Peak
                else if (prevY < y && nextY < y)
                    AddRune((Rune)'▄'); // Trough
                else if (prevY > y)
                    AddRune((Rune)'╱'); // Rising
                else if (prevY < y)
                    AddRune((Rune)'╲'); // Falling
                else
                    AddRune((Rune)'─'); // Flat
            }
            else
            {
                AddRune((Rune)'●');
            }
        }

        // Glow effect on most recent points (if theme enables it)
        if (Theme.EnableGlow && sampleCount > 2 && !_isPaused)
        {
            Driver.SetAttribute(GlowAttr);
            int lastX = LeftMargin + plotWidth - 1;
            int lastY = yPositions[sampleCount - 1];
            if (lastX >= LeftMargin && lastY >= TopMargin && lastY < TopMargin + plotHeight)
            {
                Move(lastX, lastY);
                AddRune((Rune)'█');
            }
        }
    }

    private void DrawNoSignal(int plotWidth, int plotHeight)
    {
        // Animated "NO SIGNAL" message
        Driver.SetAttribute((_frameCount % 20) < 10 ? BrightAttr : DimAttr);

        string msg = Theme.NoSignalMessage;
        int x = LeftMargin + (plotWidth - msg.Length) / 2;
        int y = TopMargin + plotHeight / 2;

        Move(x, y);
        AddStr(msg);

        Driver.SetAttribute(DimAttr);
        string hint = "Select a node to begin plotting";
        x = LeftMargin + (plotWidth - hint.Length) / 2;
        Move(x, y + 2);
        AddStr(hint);
    }

    private void DrawStatusBar(int width, int height)
    {
        int y = height - 2;

        // Status bar background
        Driver.SetAttribute(BorderAttr);
        Move(0, y);
        AddRune((Rune)'╠');
        for (int x = 1; x < width - 1; x++) AddRune((Rune)Theme.BoxHorizontal);
        AddRune((Rune)'╣');

        // Status text
        Move(0, y + 1);
        AddRune((Rune)Theme.BoxVertical);

        Driver.SetAttribute(DimAttr);

        // Key hints
        string keys = " [SPACE]";
        AddStr(keys);
        Driver.SetAttribute(_isPaused ? StatusActiveAttr : DimAttr);
        AddStr("Pause");

        Driver.SetAttribute(DimAttr);
        AddStr("  [+/-]");
        Driver.SetAttribute(!_autoScale ? StatusActiveAttr : DimAttr);
        AddStr("Scale");

        Driver.SetAttribute(DimAttr);
        AddStr("  [R]");
        Driver.SetAttribute(_autoScale ? StatusActiveAttr : DimAttr);
        AddStr("Auto");

        // Fill rest with spaces and close border
        int currentPos = 1 + keys.Length + 5 + 7 + 5 + 5 + 4;
        Driver.SetAttribute(DimAttr);

        // Current value on the right
        float currentValue = 0;
        lock (_lock)
        {
            if (_sampleCount > 0)
            {
                int lastIdx = (_writeIndex - 1 + _samples.Length) % _samples.Length;
                currentValue = _samples[lastIdx];
            }
        }

        string valueStr = $"VALUE: {currentValue:F2}";
        int valuePos = width - valueStr.Length - 3;

        // Fill gap
        for (int x = currentPos; x < valuePos; x++)
        {
            Move(x, y + 1);
            AddStr(" ");
        }

        Move(valuePos, y + 1);
        Driver.SetAttribute(BrightAttr);
        AddStr(valueStr);

        Move(width - 1, y + 1);
        Driver.SetAttribute(BorderAttr);
        AddRune((Rune)Theme.BoxVertical);

        // Bottom border
        Move(0, height - 1);
        AddRune((Rune)Theme.BoxBottomLeft);
        for (int x = 1; x < width - 1; x++) AddRune((Rune)Theme.BoxHorizontal);
        AddRune((Rune)Theme.BoxBottomRight);
    }

    private static string FormatAxisValue(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return "---";
        if (Math.Abs(value) >= 10000)
            return value.ToString("0.0e0");
        if (Math.Abs(value) >= 100)
            return value.ToString("F0");
        if (Math.Abs(value) >= 10)
            return value.ToString("F1");
        return value.ToString("F2");
    }

    private static bool TryParseValue(string? valueStr, out float value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(valueStr))
            return false;

        var str = valueStr.Trim();
        if (str.StartsWith("(") && str.EndsWith(")"))
            return false;

        return float.TryParse(str, out value);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopUpdateTimer();
            UnbindCurrentNode();
        }
        base.Dispose(disposing);
    }
}
