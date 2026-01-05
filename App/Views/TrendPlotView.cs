using Terminal.Gui;
using OpcScope.OpcUa;
using OpcScope.OpcUa.Models;
using OpcScope.App.Themes;
using OpcScope.Utilities;
using System.Text;
using Attribute = Terminal.Gui.Attribute;
using Rune = System.Text.Rune;
using ThemeManager = OpcScope.App.Themes.ThemeManager;

namespace OpcScope.App.Views;

/// <summary>
/// Real-time 2D scrolling oscilloscope-style plot.
/// Supports multiple themes via ThemeManager.
/// </summary>
public class TrendPlotView : View
{
    // === Cached theme and attributes ===
    private RetroTheme _currentTheme = null!; // Initialized in constructor
    private Attribute _brightAttr;
    private Attribute _normalAttr;
    private Attribute _dimAttr;
    private Attribute _gridAttr;
    private Attribute _borderAttr;
    private Attribute _statusActiveAttr;
    private Attribute _statusInactiveAttr;
    private Attribute _glowAttr;
    private Attribute _backgroundAttr;
    private readonly object _themeLock = new();

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

        // Initialize cached theme
        lock (_themeLock)
        {
            _currentTheme = ThemeManager.Current;
            CacheThemeAttributes();
        }

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;

        CanFocus = true;
        WantMousePositionReports = false;
    }

    private void CacheThemeAttributes()
    {
        // Note: Must be called inside _themeLock to ensure atomic updates
        _brightAttr = _currentTheme.BrightAttr;
        _normalAttr = _currentTheme.NormalAttr;
        _dimAttr = _currentTheme.DimAttr;
        _gridAttr = _currentTheme.GridAttr;
        _borderAttr = _currentTheme.BorderAttr;
        _statusActiveAttr = _currentTheme.StatusActiveAttr;
        _statusInactiveAttr = _currentTheme.StatusInactiveAttr;
        _glowAttr = _currentTheme.GlowAttr;
        _backgroundAttr = new(_currentTheme.Background, _currentTheme.Background);
    }

    private void OnThemeChanged(RetroTheme newTheme)
    {
        lock (_themeLock)
        {
            _currentTheme = newTheme;
            CacheThemeAttributes();
        }
        
        try
        {
            Application.Invoke(() => SetNeedsLayout());
        }
        catch (InvalidOperationException)
        {
            // Application may not be initialized yet - ignore
        }
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
        Driver.SetAttribute(_backgroundAttr);
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

        // Draw in order: header, frame, grid, data, status
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

    private void DrawHeader(int width)
    {
        // Industrial header bar
        Driver.SetAttribute(_borderAttr);
        Move(0, 0);
        AddRune((Rune)_currentTheme.BoxTopLeft);
        for (int x = 1; x < width - 1; x++) AddRune((Rune)_currentTheme.BoxHorizontal);
        AddRune((Rune)_currentTheme.BoxTopRight);

        // Title with signal name
        string title = _boundNode?.DisplayName?.ToUpperInvariant() ?? "SCOPE";
        if (title.Length > 20) title = title[..20];

        Move(2, 0);
        Driver.SetAttribute(_brightAttr);
        AddStr($"{_currentTheme.BoxTitleLeft} {title} {_currentTheme.BoxTitleRight}");

        // Status indicators on right
        int rightPos = width - 25;
        if (rightPos > title.Length + 10)
        {
            Move(rightPos, 0);
            Driver.SetAttribute(_borderAttr);
            AddStr($"{_currentTheme.BoxTitleLeft}");

            // LIVE/HOLD indicator
            if (_isPaused)
            {
                Driver.SetAttribute(_statusInactiveAttr);
                AddStr(" HOLD ");
            }
            else
            {
                Driver.SetAttribute(_statusActiveAttr);
                AddStr(" LIVE ");
            }

            Driver.SetAttribute(_borderAttr);
            AddStr("│");

            // Blinking activity indicator
            if (!_isPaused && (_frameCount % 10) < 5)
            {
                Driver.SetAttribute(_statusActiveAttr);
                AddStr("●");
            }
            else
            {
                Driver.SetAttribute(_statusInactiveAttr);
                AddStr("○");
            }

            Driver.SetAttribute(_borderAttr);
            AddStr($"{_currentTheme.BoxTitleRight}");
        }

        // Second header line with technical info
        Move(0, 1);
        Driver.SetAttribute(_borderAttr);
        AddRune((Rune)_currentTheme.BoxVertical);

        Driver.SetAttribute(_dimAttr);
        string techInfo = $" CH1  SCALE:{(_autoScale ? "AUTO" : $"{_scaleMultiplier:F1}X")}  ";
        lock (_lock)
        {
            techInfo += $"SAMPLES:{_sampleCount,4}  ";
        }
        techInfo += $"RANGE:[{FormatAxisValue(_visibleMin)},{FormatAxisValue(_visibleMax)}]";

        AddStr(techInfo.PadRight(width - 2));

        Move(width - 1, 1);
        Driver.SetAttribute(_borderAttr);
        AddRune((Rune)_currentTheme.BoxVertical);
    }

    private void DrawIndustrialFrame(int width, int height, int plotWidth, int plotHeight)
    {
        int plotTop = TopMargin - 1;
        int plotBottom = TopMargin + plotHeight;
        int plotLeft = LeftMargin - 1;
        int plotRight = LeftMargin + plotWidth;

        Driver.SetAttribute(_borderAttr);

        // Top border of plot area
        Move(plotLeft, plotTop);
        AddRune((Rune)_currentTheme.BoxTopLeft);
        for (int x = plotLeft + 1; x < plotRight; x++)
        {
            // Tick marks every 10 columns
            if ((x - LeftMargin) % 10 == 0 && x < plotRight - 1)
                AddRune((Rune)_currentTheme.TickHorizontal);
            else
                AddRune((Rune)_currentTheme.BoxHorizontal);
        }
        AddRune((Rune)_currentTheme.BoxTopRight);

        // Side borders
        for (int y = plotTop + 1; y < plotBottom; y++)
        {
            Move(plotLeft, y);
            // Tick marks every 4 rows
            if ((y - TopMargin) % 4 == 0)
                AddRune((Rune)_currentTheme.TickVertical);
            else
                AddRune((Rune)_currentTheme.BoxVertical);

            Move(plotRight, y);
            if ((y - TopMargin) % 4 == 0)
                AddRune((Rune)_currentTheme.TickVerticalRight);
            else
                AddRune((Rune)_currentTheme.BoxVertical);
        }

        // Bottom border
        Move(plotLeft, plotBottom);
        AddRune((Rune)_currentTheme.BoxBottomLeft);
        for (int x = plotLeft + 1; x < plotRight; x++)
        {
            if ((x - LeftMargin) % 10 == 0 && x < plotRight - 1)
                AddRune((Rune)_currentTheme.TickHorizontalBottom);
            else
                AddRune((Rune)_currentTheme.BoxHorizontal);
        }
        AddRune((Rune)_currentTheme.BoxBottomRight);

        // Corner ornaments
        Driver.SetAttribute(_dimAttr);
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
        Driver.SetAttribute(_gridAttr);

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
            Driver.SetAttribute(_dimAttr);
            for (int x = 0; x < plotWidth; x++)
            {
                Move(LeftMargin + x, TopMargin + centerRow);
                AddRune((Rune)'─');
            }
        }
    }

    private void DrawYAxisLabels(int plotHeight)
    {
        Driver.SetAttribute(_normalAttr);

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

            Driver.SetAttribute(_dimAttr);
            Move(1, TopMargin + plotHeight * 3 / 4);
            AddStr(FormatAxisValue(q1).PadLeft(LeftMargin - 2));
            Move(1, TopMargin + plotHeight / 4);
            AddStr(FormatAxisValue(q3).PadLeft(LeftMargin - 2));
        }
    }

    /// <summary>
    /// Draws the waveform using Braille characters for smooth sub-pixel rendering.
    /// Each Braille character provides a 2×4 dot matrix, giving 4x vertical resolution.
    /// </summary>
    private void DrawWaveform(float[] samples, int sampleCount, int plotWidth, int plotHeight)
    {
        float range = _visibleMax - _visibleMin;
        if (range <= 0) range = 1;

        // Braille gives us 4 sub-pixels per cell vertically
        int subPixelHeight = plotHeight * 4;
        int startX = LeftMargin + (plotWidth - sampleCount);

        // Pre-calculate all sub-pixel Y positions
        int[] subYPositions = new int[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float norm = Math.Clamp((samples[i] - _visibleMin) / range, 0, 1);
            subYPositions[i] = (int)((1 - norm) * (subPixelHeight - 1));
        }

        // Build a sparse map of (x, cellY) -> braille dot pattern
        // This allows us to combine multiple dots into single characters
        var cellPatterns = new Dictionary<(int x, int cellY), int>();

        for (int i = 0; i < sampleCount; i++)
        {
            int x = startX + i;
            if (x < LeftMargin || x >= LeftMargin + plotWidth) continue;

            int subY = subYPositions[i];

            // Determine vertical span to connect with previous sample (Bresenham-style)
            int minSubY = subY, maxSubY = subY;
            if (i > 0)
            {
                int prevSubY = subYPositions[i - 1];
                minSubY = Math.Min(subY, prevSubY);
                maxSubY = Math.Max(subY, prevSubY);
            }

            // Fill in all sub-pixels in the vertical span
            for (int sy = minSubY; sy <= maxSubY; sy++)
            {
                int cellY = sy / 4;
                int dotRow = sy % 4;

                // Braille dot pattern: dots are numbered 1-8 in a 2x4 grid
                // We use column 0 (left column): dots 1,2,3,7 for rows 0,1,2,3
                // Bit positions: row0=0x01, row1=0x02, row2=0x04, row3=0x40
                int dotBit = dotRow switch
                {
                    0 => 0x01,
                    1 => 0x02,
                    2 => 0x04,
                    3 => 0x40,
                    _ => 0
                };

                var key = (x, cellY);
                cellPatterns.TryGetValue(key, out int existing);
                cellPatterns[key] = existing | dotBit;
            }
        }

        // Render all cells
        const int BrailleBase = 0x2800;
        int lastColorIndex = -1;

        foreach (var kvp in cellPatterns.OrderBy(k => k.Key.x).ThenBy(k => k.Key.cellY))
        {
            int x = kvp.Key.x;
            int cellY = kvp.Key.cellY;
            int pattern = kvp.Value;

            int screenY = TopMargin + cellY;
            if (screenY < TopMargin || screenY >= TopMargin + plotHeight) continue;

            // Calculate distance from leading edge for color selection
            int sampleIndex = x - startX;
            int colorIndex = sampleIndex >= sampleCount - 3 && !_isPaused && _currentTheme.EnableGlow ? 2
                           : sampleIndex >= sampleCount - 8 ? 1
                           : 0;

            if (colorIndex != lastColorIndex)
            {
                Driver.SetAttribute(colorIndex == 2 ? _glowAttr : colorIndex == 1 ? _brightAttr : _normalAttr);
                lastColorIndex = colorIndex;
            }

            Move(x, screenY);
            AddRune(new Rune(BrailleBase + pattern));
        }
    }

    private void DrawNoSignal(int plotWidth, int plotHeight)
    {
        // Animated "NO SIGNAL" message
        Driver.SetAttribute((_frameCount % 20) < 10 ? _brightAttr : _dimAttr);

        string msg = _currentTheme.NoSignalMessage;
        int x = LeftMargin + (plotWidth - msg.Length) / 2;
        int y = TopMargin + plotHeight / 2;

        Move(x, y);
        AddStr(msg);

        Driver.SetAttribute(_dimAttr);
        string hint = "Select a node to begin plotting";
        x = LeftMargin + (plotWidth - hint.Length) / 2;
        Move(x, y + 2);
        AddStr(hint);
    }

    private void DrawStatusBar(int width, int height)
    {
        int y = height - 2;

        // Status bar background
        Driver.SetAttribute(_borderAttr);
        Move(0, y);
        AddRune((Rune)_currentTheme.BoxLeftT);
        for (int x = 1; x < width - 1; x++) AddRune((Rune)_currentTheme.BoxHorizontal);
        AddRune((Rune)_currentTheme.BoxRightT);

        // Status text
        Move(0, y + 1);
        AddRune((Rune)_currentTheme.BoxVertical);

        Driver.SetAttribute(_dimAttr);

        // Key hints
        string keys = " [SPACE]";
        AddStr(keys);
        Driver.SetAttribute(_isPaused ? _statusActiveAttr : _dimAttr);
        AddStr("Pause");

        Driver.SetAttribute(_dimAttr);
        AddStr("  [+/-]");
        Driver.SetAttribute(!_autoScale ? _statusActiveAttr : _dimAttr);
        AddStr("Scale");

        Driver.SetAttribute(_dimAttr);
        AddStr("  [R]");
        Driver.SetAttribute(_autoScale ? _statusActiveAttr : _dimAttr);
        AddStr("Auto");

        // Fill rest with spaces and close border
        int currentPos = 1 + keys.Length + 5 + 7 + 5 + 5 + 4;
        Driver.SetAttribute(_dimAttr);

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
        Driver.SetAttribute(_brightAttr);
        AddStr(valueStr);

        Move(width - 1, y + 1);
        Driver.SetAttribute(_borderAttr);
        AddRune((Rune)_currentTheme.BoxVertical);

        // Bottom border
        Move(0, height - 1);
        AddRune((Rune)_currentTheme.BoxBottomLeft);
        for (int x = 1; x < width - 1; x++) AddRune((Rune)_currentTheme.BoxHorizontal);
        AddRune((Rune)_currentTheme.BoxBottomRight);
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
            ThemeManager.ThemeChanged -= OnThemeChanged;
            StopUpdateTimer();
            UnbindCurrentNode();
        }
        base.Dispose(disposing);
    }
}
