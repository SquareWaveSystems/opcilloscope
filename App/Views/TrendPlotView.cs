using Terminal.Gui;
using OpcScope.OpcUa;
using OpcScope.OpcUa.Models;
using OpcScope.Utilities;

namespace OpcScope.App.Views;

/// <summary>
/// Real-time 2D scrolling line plot for visualizing monitored variable values over time.
/// Uses Braille characters for sub-cell resolution rendering.
/// </summary>
public class TrendPlotView : View
{
    // Ring buffer for samples (preallocated)
    private readonly float[] _samples;
    private int _writeIndex;
    private int _sampleCount;
    private readonly object _lock = new();

    // Auto-scale tracking
    private float _visibleMin = float.MaxValue;
    private float _visibleMax = float.MinValue;
    private float _manualMin;
    private float _manualMax;
    private bool _autoScale = true;
    private float _scaleMultiplier = 1.0f;

    // State
    private bool _isPaused;
    private MonitoredNode? _boundNode;
    private SubscriptionManager? _subscriptionManager;
    private Action<MonitoredNode>? _valueChangedHandler;
    private object? _timerToken;
    private float _demoPhase;

    // Braille constants - each character is 2 dots wide x 4 dots high
    private const int DotsPerCellX = 2;
    private const int DotsPerCellY = 4;

    // Left margin for Y-axis labels
    private const int LeftMargin = 8;

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
    public void AddSample(float value)
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

        SetNeedsDisplay();
    }

    /// <summary>
    /// Binds using a callback for value changes (alternative binding method).
    /// </summary>
    public void BindToValueSource(Func<float> valueSource, string displayName)
    {
        UnbindCurrentNode();
        _boundNode = new MonitoredNode { DisplayName = displayName };
        SetNeedsDisplay();
    }

    private void OnBoundNodeValueChanged(MonitoredNode node)
    {
        if (_boundNode != null && node.ClientHandle == _boundNode.ClientHandle)
        {
            if (TryParseValue(node.Value, out var value))
            {
                AddSample(value);
            }
        }
    }

    private void UnbindCurrentNode()
    {
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
        SetNeedsDisplay();
    }

    /// <summary>
    /// Starts the demo sine wave animation.
    /// </summary>
    public void StartDemoMode()
    {
        UnbindCurrentNode();
        _boundNode = new MonitoredNode { DisplayName = "Demo Sine Wave" };
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
        if (!_isPaused && _boundNode?.DisplayName == "Demo Sine Wave")
        {
            // Generate demo sine wave
            _demoPhase += 0.15f;
            var value = (float)(50 + 40 * Math.Sin(_demoPhase));
            AddSample(value);
        }

        SetNeedsDisplay();
        return true; // Keep timer running
    }

    /// <summary>
    /// Toggles pause state.
    /// </summary>
    public void TogglePause()
    {
        _isPaused = !_isPaused;
        PauseStateChanged?.Invoke(_isPaused);
        SetNeedsDisplay();
    }

    /// <summary>
    /// Increases vertical scale (zoom in).
    /// </summary>
    public void IncreaseScale()
    {
        _autoScale = false;
        _scaleMultiplier *= 1.2f;
        SetNeedsDisplay();
    }

    /// <summary>
    /// Decreases vertical scale (zoom out).
    /// </summary>
    public void DecreaseScale()
    {
        _autoScale = false;
        _scaleMultiplier /= 1.2f;
        if (_scaleMultiplier < 0.1f) _scaleMultiplier = 0.1f;
        SetNeedsDisplay();
    }

    /// <summary>
    /// Resets to auto-scale mode.
    /// </summary>
    public void ResetScale()
    {
        _autoScale = true;
        _scaleMultiplier = 1.0f;
        SetNeedsDisplay();
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

    protected override void OnDrawContent(Rectangle viewport)
    {
        base.OnDrawContent(viewport);

        var driver = Application.Driver;
        if (driver == null) return;

        // Calculate plot area
        int plotWidth = viewport.Width - LeftMargin - 1;
        int plotHeight = viewport.Height - 2; // Leave room for title and status

        if (plotWidth < 4 || plotHeight < 2) return;

        // Get samples to display
        float[] displaySamples;
        int displayCount;
        lock (_lock)
        {
            displayCount = Math.Min(_sampleCount, plotWidth);
            displaySamples = new float[displayCount];

            if (displayCount > 0)
            {
                // Copy samples from ring buffer (newest on right)
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
            // Use manual scale with multiplier
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

        // Add some padding to auto-scale
        float padding = (_visibleMax - _visibleMin) * 0.05f;
        if (padding < 0.1f) padding = 0.1f;
        _visibleMin -= padding;
        _visibleMax += padding;

        // Draw title/status
        DrawTitle(driver, viewport);

        // Draw Y-axis labels
        DrawYAxisLabels(driver, viewport, plotHeight);

        // Draw plot border
        DrawPlotBorder(driver, viewport, plotHeight);

        // Draw the data
        if (displayCount > 0)
        {
            DrawPlotData(driver, viewport, displaySamples, displayCount, plotWidth, plotHeight);
        }
        else
        {
            // No data message
            string noData = "No data";
            int x = LeftMargin + (plotWidth - noData.Length) / 2;
            int y = 1 + plotHeight / 2;
            Move(x, y);
            driver.SetAttribute(new Attribute(Color.Gray, Color.Black));
            driver.AddStr(noData);
        }

        // Draw status line
        DrawStatusLine(driver, viewport);
    }

    private void DrawTitle(IConsoleDriver driver, Rectangle viewport)
    {
        string title = _boundNode?.DisplayName ?? "Trend Plot";
        if (_isPaused) title += " [PAUSED]";

        Move(LeftMargin, 0);
        driver.SetAttribute(new Attribute(Color.White, Color.Black));
        driver.AddStr(title.Length > viewport.Width - LeftMargin
            ? title[..(viewport.Width - LeftMargin - 3)] + "..."
            : title);
    }

    private void DrawYAxisLabels(IConsoleDriver driver, Rectangle viewport, int plotHeight)
    {
        driver.SetAttribute(new Attribute(Color.Cyan, Color.Black));

        // Max value at top
        string maxStr = FormatAxisValue(_visibleMax);
        Move(0, 1);
        driver.AddStr(maxStr.PadLeft(LeftMargin - 1));

        // Mid value
        if (plotHeight > 4)
        {
            float midVal = (_visibleMin + _visibleMax) / 2;
            string midStr = FormatAxisValue(midVal);
            Move(0, 1 + plotHeight / 2);
            driver.AddStr(midStr.PadLeft(LeftMargin - 1));
        }

        // Min value at bottom
        string minStr = FormatAxisValue(_visibleMin);
        Move(0, plotHeight);
        driver.AddStr(minStr.PadLeft(LeftMargin - 1));
    }

    private void DrawPlotBorder(IConsoleDriver driver, Rectangle viewport, int plotHeight)
    {
        driver.SetAttribute(new Attribute(Color.DarkGray, Color.Black));

        // Left border
        for (int y = 1; y <= plotHeight; y++)
        {
            Move(LeftMargin - 1, y);
            driver.AddRune((Rune)'│');
        }

        // Bottom border
        Move(LeftMargin - 1, plotHeight + 1);
        driver.AddRune((Rune)'└');
        for (int x = LeftMargin; x < viewport.Width - 1; x++)
        {
            driver.AddRune((Rune)'─');
        }
    }

    private void DrawPlotData(IConsoleDriver driver, Rectangle viewport, float[] samples,
        int sampleCount, int plotWidth, int plotHeight)
    {
        driver.SetAttribute(new Attribute(Color.Green, Color.Black));

        float range = _visibleMax - _visibleMin;
        if (range <= 0) range = 1;

        // We'll use simple block characters for now (█▀▄ )
        // Each cell can represent: full block, upper half, lower half, or empty

        // Calculate dots height (each cell is 2 dots vertically for our simple approach)
        int dotsHeight = plotHeight * 2;

        for (int i = 0; i < sampleCount && i < plotWidth; i++)
        {
            float normalized = (samples[i] - _visibleMin) / range;
            normalized = Math.Clamp(normalized, 0, 1);

            // Map to dot position (0 = bottom, dotsHeight-1 = top)
            int dotY = (int)(normalized * (dotsHeight - 1));

            // Convert to cell coordinates
            int cellX = LeftMargin + i;
            int cellY = plotHeight - (dotY / 2);

            if (cellX >= viewport.Width - 1 || cellY < 1 || cellY > plotHeight)
                continue;

            Move(cellX, cellY);

            // Determine which half of the cell the point is in
            bool upperHalf = (dotY % 2) == 1;

            if (upperHalf)
            {
                driver.AddRune((Rune)'▀');
            }
            else
            {
                driver.AddRune((Rune)'▄');
            }
        }

        // Draw connecting lines between samples using a different approach
        // For better visualization, draw vertical lines between consecutive samples
        if (sampleCount > 1)
        {
            driver.SetAttribute(new Attribute(Color.BrightGreen, Color.Black));

            for (int i = 0; i < sampleCount - 1 && i < plotWidth - 1; i++)
            {
                float norm1 = (samples[i] - _visibleMin) / range;
                float norm2 = (samples[i + 1] - _visibleMin) / range;
                norm1 = Math.Clamp(norm1, 0, 1);
                norm2 = Math.Clamp(norm2, 0, 1);

                int y1 = plotHeight - (int)(norm1 * (plotHeight - 1));
                int y2 = plotHeight - (int)(norm2 * (plotHeight - 1));

                int cellX = LeftMargin + i;

                // Draw vertical line segment if there's a gap
                int minY = Math.Min(y1, y2);
                int maxY = Math.Max(y1, y2);

                for (int y = minY; y <= maxY; y++)
                {
                    if (y >= 1 && y <= plotHeight && cellX < viewport.Width - 1)
                    {
                        Move(cellX, y);
                        if (y == y1 || y == y2)
                        {
                            driver.AddRune((Rune)'●');
                        }
                        else
                        {
                            driver.AddRune((Rune)'│');
                        }
                    }
                }
            }
        }
    }

    private void DrawStatusLine(IConsoleDriver driver, Rectangle viewport)
    {
        driver.SetAttribute(new Attribute(Color.Gray, Color.Black));

        int y = viewport.Height - 1;
        Move(0, y);

        string status = $"[Space]=Pause [+/-]=Scale [R]=Reset";
        if (!_autoScale)
        {
            status += $" | Scale: {_scaleMultiplier:F1}x";
        }

        lock (_lock)
        {
            status += $" | Samples: {_sampleCount}";
        }

        driver.AddStr(status.Length > viewport.Width ? status[..viewport.Width] : status);
    }

    private static string FormatAxisValue(float value)
    {
        if (Math.Abs(value) >= 1000)
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

        // Handle common formats
        var str = valueStr.Trim();

        // Remove common prefixes
        if (str.StartsWith("(") && str.EndsWith(")"))
            return false; // e.g., "(pending)"

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

