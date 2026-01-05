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
/// Real-time 2D scrolling oscilloscope-style plot with retro-futuristic CRT aesthetic.
/// Inspired by 1970s-80s industrial control displays and cassette futurism.
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
    private Attribute _scanlineAttr;
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

    // === 3D Depth Effect ===
    private const int MaxDepth = 20;
    private const int MinDepth = 1;
    private const int DefaultDepth = 8;

    // Glyph ladder for depth fading (brightest to dimmest)
    private static readonly char[] DepthGlyphs = { '█', '▓', '▒', '░', '·' };

    // Frame history buffer: [depthIndex][sampleIndex]
    private readonly float[][] _frameHistory;
    private int _currentFrameIndex;
    private int _depthCount = DefaultDepth;
    private bool _3dEnabled = false;

    // Preallocated arrays for rendering (avoid allocation in draw loop)
    private readonly int[] _frameOrder;
    private readonly HashSet<(int x, int y)> _cellsToFill = new();

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

        // Pre-allocate frame history for 3D depth effect
        _frameHistory = new float[MaxDepth][];
        for (int i = 0; i < MaxDepth; i++)
        {
            _frameHistory[i] = new float[200];
        }
        _frameOrder = new int[MaxDepth];

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
        _scanlineAttr = _currentTheme.ScanlineAttr;
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

            // Clear frame history
            for (int i = 0; i < MaxDepth; i++)
            {
                Array.Clear(_frameHistory[i]);
            }
            _currentFrameIndex = 0;
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

        // Capture current frame for 3D depth effect
        if (_3dEnabled && !_isPaused)
        {
            CaptureFrameHistory();
        }

        SetNeedsLayout();
        return true; // Keep timer running
    }

    /// <summary>
    /// Captures the current ring buffer state into the frame history for 3D rendering.
    /// </summary>
    private void CaptureFrameHistory()
    {
        lock (_lock)
        {
            // Copy current ring buffer to the current frame slot
            Array.Copy(_samples, _frameHistory[_currentFrameIndex], _samples.Length);

            // Advance to next frame slot (circular)
            _currentFrameIndex = (_currentFrameIndex + 1) % _depthCount;
        }
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

    /// <summary>
    /// Toggles 3D depth effect on/off.
    /// </summary>
    public void Toggle3DEffect()
    {
        _3dEnabled = !_3dEnabled;

        // Initialize frame history when enabling 3D
        if (_3dEnabled)
        {
            // Fill all frames with current buffer state
            lock (_lock)
            {
                _currentFrameIndex = 0;
                for (int i = 0; i < _depthCount; i++)
                {
                    Array.Copy(_samples, _frameHistory[i], _samples.Length);
                }
            }
        }

        SetNeedsLayout();
    }

    /// <summary>
    /// Increases 3D depth (more trailing frames).
    /// </summary>
    public void IncreaseDepth()
    {
        lock (_lock)
        {
            if (_depthCount < MaxDepth)
            {
                _depthCount++;
            }
        }
        SetNeedsLayout();
    }

    /// <summary>
    /// Decreases 3D depth (fewer trailing frames).
    /// </summary>
    public void DecreaseDepth()
    {
        lock (_lock)
        {
            if (_depthCount > MinDepth)
            {
                _depthCount--;
                // Ensure current frame index is within bounds
                if (_currentFrameIndex >= _depthCount)
                {
                    _currentFrameIndex = 0;
                }
            }
        }
        SetNeedsLayout();
    }

    /// <summary>
    /// Gets whether 3D depth effect is enabled.
    /// </summary>
    public bool Is3DEnabled => _3dEnabled;

    /// <summary>
    /// Gets the current depth count.
    /// </summary>
    public int DepthCount => _depthCount;

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

            // 3D depth effect controls
            case (KeyCode)'d':
            case (KeyCode)'D':
                Toggle3DEffect();
                return true;

            case (KeyCode)'[':
                DecreaseDepth();
                return true;

            case (KeyCode)']':
                IncreaseDepth();
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

        // Draw in order: grid, border, data, status
        DrawScanlines(viewport.Width, viewport.Height);
        DrawHeader(viewport.Width);
        DrawIndustrialFrame(viewport.Width, viewport.Height, plotWidth, plotHeight);
        DrawGrid(plotWidth, plotHeight);
        DrawYAxisLabels(plotHeight);

        if (displayCount > 0)
        {
            if (_3dEnabled && _depthCount > 1)
            {
                // Draw 3D depth effect: historical frames from oldest to newest (painter's algorithm)
                Draw3DWaveform(plotWidth, plotHeight, displayCount);
            }
            else
            {
                // Standard 2D waveform
                DrawWaveform(displaySamples, displayCount, plotWidth, plotHeight);
            }
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
        if (!_currentTheme.EnableScanlines) return;

        Driver.SetAttribute(_scanlineAttr);
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
        if (_3dEnabled)
        {
            techInfo += $"  3D:{_depthCount}";
        }

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

    private void DrawWaveform(float[] samples, int sampleCount, int plotWidth, int plotHeight)
    {
        float range = _visibleMax - _visibleMin;
        if (range <= 0) range = 1;

        // Track which sub-pixels are lit for each column
        // Using bit arrays for efficient storage: each column has plotHeight * 2 sub-pixels
        var columnLit = new Dictionary<int, HashSet<int>>();

        // Convert samples to sub-pixel coordinates and draw lines between them
        int subPixelHeight = plotHeight * 2;

        for (int i = 0; i < sampleCount - 1; i++)
        {
            // Calculate screen X positions for current and next sample
            int x1 = LeftMargin + (plotWidth - sampleCount) + i;
            int x2 = LeftMargin + (plotWidth - sampleCount) + i + 1;

            // Calculate sub-pixel Y positions (0 = top, subPixelHeight-1 = bottom)
            float normalized1 = Math.Clamp((samples[i] - _visibleMin) / range, 0, 1);
            float normalized2 = Math.Clamp((samples[i + 1] - _visibleMin) / range, 0, 1);

            int y1 = (int)((1 - normalized1) * (subPixelHeight - 1));
            int y2 = (int)((1 - normalized2) * (subPixelHeight - 1));

            // Draw line from (x1, y1) to (x2, y2) using Bresenham-style algorithm
            int dx = Math.Abs(x2 - x1);
            int dy = Math.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = y1 < y2 ? 1 : -1;
            int err = dx - dy;

            int x = x1;
            int y = y1;

            while (true)
            {
                // Mark this sub-pixel as lit
                if (x >= LeftMargin && x < LeftMargin + plotWidth)
                {
                    if (!columnLit.ContainsKey(x))
                        columnLit[x] = new HashSet<int>();
                    columnLit[x].Add(y);
                }

                if (x == x2 && y == y2) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }
        }

        // Draw the last sample point
        if (sampleCount > 0)
        {
            int x = LeftMargin + (plotWidth - sampleCount) + sampleCount - 1;
            float normalized = Math.Clamp((samples[sampleCount - 1] - _visibleMin) / range, 0, 1);
            int y = (int)((1 - normalized) * (subPixelHeight - 1));

            if (x >= LeftMargin && x < LeftMargin + plotWidth)
            {
                if (!columnLit.ContainsKey(x))
                    columnLit[x] = new HashSet<int>();
                columnLit[x].Add(y);
            }
        }

        // Render the lit sub-pixels as block characters
        foreach (var kvp in columnLit)
        {
            int x = kvp.Key;
            var litSubPixels = kvp.Value;

            // Determine sample index for color selection
            int sampleIdx = x - LeftMargin - (plotWidth - sampleCount);

            // Group sub-pixels into cells and determine which block character to use
            var cellsToFill = new Dictionary<int, (bool top, bool bottom)>();

            foreach (int subY in litSubPixels)
            {
                int cellY = subY / 2;
                bool isTop = (subY % 2 == 0);

                if (!cellsToFill.ContainsKey(cellY))
                    cellsToFill[cellY] = (false, false);

                var cell = cellsToFill[cellY];
                cellsToFill[cellY] = isTop ? (true, cell.bottom) : (cell.top, true);
            }

            // Draw each cell
            foreach (var cell in cellsToFill)
            {
                int cellY = cell.Key;
                int screenY = TopMargin + cellY;

                if (screenY < TopMargin || screenY >= TopMargin + plotHeight) continue;

                Move(x, screenY);

                // Choose color based on position (brighter near the leading edge)
                // Use theme-aware colors for glow effect
                if (sampleIdx >= sampleCount - 3 && !_isPaused && _currentTheme.EnableGlow)
                    Driver.SetAttribute(_glowAttr);
                else if (sampleIdx >= sampleCount - 8)
                    Driver.SetAttribute(_brightAttr);
                else
                    Driver.SetAttribute(_normalAttr);

                // Select the right block character
                var (fillTop, fillBottom) = cell.Value;
                if (fillTop && fillBottom)
                    AddRune((Rune)'█');  // Full block
                else if (fillTop)
                    AddRune((Rune)'▀');  // Upper half
                else if (fillBottom)
                    AddRune((Rune)'▄');  // Lower half
            }
        }
    }

    /// <summary>
    /// Draws the 3D depth effect with multiple historical frames.
    /// Uses painter's algorithm: draws oldest frames first (furthest back), newest last (foreground).
    /// </summary>
    private void Draw3DWaveform(int plotWidth, int plotHeight, int displayCount)
    {
        float range = _visibleMax - _visibleMin;
        if (range <= 0) range = 1;

        // Copy frame indices and depth count under lock to avoid race conditions
        int depthCount;
        int currentFrameIndex;
        lock (_lock)
        {
            depthCount = _depthCount;
            currentFrameIndex = _currentFrameIndex;
        }

        // Calculate which frame indices to draw (oldest to newest)
        // currentFrameIndex points to where the NEXT frame will be written,
        // so the oldest frame is at currentFrameIndex, newest is at currentFrameIndex - 1
        for (int i = 0; i < depthCount; i++)
        {
            // Start from oldest (at currentFrameIndex) going forward
            _frameOrder[i] = (currentFrameIndex + i) % depthCount;
        }

        // Draw frames from oldest (most offset, dimmest) to newest (no offset, brightest)
        for (int depthIdx = 0; depthIdx < depthCount; depthIdx++)
        {
            int frameIdx = _frameOrder[depthIdx];
            int depth = depthCount - 1 - depthIdx; // depth=0 for newest, depth=depthCount-1 for oldest

            // Calculate offset: each depth level offsets by (-1, +1)
            int offsetX = -depth;
            int offsetY = depth;

            // Select glyph based on depth (fading effect)
            char glyph = GetDepthGlyph(depth, depthCount);

            // Select color attribute based on depth
            Attribute colorAttr = GetDepthColor(depth, depthCount);

            // Get samples for this frame
            // Note: Reading from _frameHistory without lock is safe because:
            // 1. Arrays are preallocated and never replaced
            // 2. Float reads are atomic
            // 3. Worst case is seeing a partially updated frame for one render (acceptable for visualization)
            float[] frameSamples = _frameHistory[frameIdx];

            // Draw this frame at the specified depth
            DrawWaveformAtDepth(frameSamples, displayCount, plotWidth, plotHeight,
                               offsetX, offsetY, glyph, colorAttr, range, depth == 0);
        }
    }

    /// <summary>
    /// Gets the appropriate glyph for the given depth level.
    /// </summary>
    private static char GetDepthGlyph(int depth, int maxDepth)
    {
        if (depth == 0) return '█'; // Current frame always uses full block

        // Map depth to glyph ladder: █ ▓ ▒ ░ ·
        // Skip first glyph (█) for historical frames; normalize depth to [0, 1)
        float t = (float)depth / Math.Max(1, maxDepth);
        int glyphIdx = 1 + (int)(t * (DepthGlyphs.Length - 1));
        glyphIdx = Math.Clamp(glyphIdx, 1, DepthGlyphs.Length - 1);
        return DepthGlyphs[glyphIdx];
    }

    /// <summary>
    /// Gets the appropriate color attribute for the given depth level.
    /// </summary>
    private Attribute GetDepthColor(int depth, int maxDepth)
    {
        if (depth == 0) return _brightAttr;
        if (depth == 1) return _normalAttr;

        // Fade to dim for deeper frames
        float t = (float)depth / Math.Max(1, maxDepth - 1);
        if (t < 0.5f) return _normalAttr;
        return _dimAttr;
    }

    /// <summary>
    /// Draws a waveform at a specific depth with offset and custom glyph.
    /// </summary>
    private void DrawWaveformAtDepth(float[] samples, int sampleCount, int plotWidth, int plotHeight,
                                     int offsetX, int offsetY, char glyph, Attribute colorAttr,
                                     float range, bool isForeground)
    {
        // Reuse preallocated set to track which cells are filled for this frame
        _cellsToFill.Clear();

        int subPixelHeight = plotHeight * 2;

        // Draw lines between consecutive samples
        for (int i = 0; i < sampleCount - 1; i++)
        {
            // Calculate base screen X positions
            int baseX1 = LeftMargin + (plotWidth - sampleCount) + i;
            int baseX2 = LeftMargin + (plotWidth - sampleCount) + i + 1;

            // Apply X offset
            int x1 = baseX1 + offsetX;
            int x2 = baseX2 + offsetX;

            // Calculate sub-pixel Y positions
            float normalized1 = Math.Clamp((samples[i] - _visibleMin) / range, 0, 1);
            float normalized2 = Math.Clamp((samples[i + 1] - _visibleMin) / range, 0, 1);

            int subY1 = (int)((1 - normalized1) * (subPixelHeight - 1));
            int subY2 = (int)((1 - normalized2) * (subPixelHeight - 1));

            // Apply Y offset (in sub-pixels, so multiply by 2)
            subY1 += offsetY * 2;
            subY2 += offsetY * 2;

            // Bresenham line drawing
            int dx = Math.Abs(x2 - x1);
            int dy = Math.Abs(subY2 - subY1);
            int sx = x1 < x2 ? 1 : -1;
            int sy = subY1 < subY2 ? 1 : -1;
            int err = dx - dy;

            int x = x1;
            int subY = subY1;

            while (true)
            {
                // Convert sub-pixel Y to cell Y
                int cellY = subY / 2;
                int screenY = TopMargin + cellY;

                // Check bounds (considering offset)
                if (x >= LeftMargin && x < LeftMargin + plotWidth &&
                    screenY >= TopMargin && screenY < TopMargin + plotHeight)
                {
                    _cellsToFill.Add((x, screenY));
                }

                if (x == x2 && subY == subY2) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    subY += sy;
                }
            }
        }

        // Draw the last sample point
        if (sampleCount > 0)
        {
            int x = LeftMargin + (plotWidth - sampleCount) + sampleCount - 1 + offsetX;
            float normalized = Math.Clamp((samples[sampleCount - 1] - _visibleMin) / range, 0, 1);
            int subY = (int)((1 - normalized) * (subPixelHeight - 1)) + offsetY * 2;
            int cellY = subY / 2;
            int screenY = TopMargin + cellY;

            if (x >= LeftMargin && x < LeftMargin + plotWidth &&
                screenY >= TopMargin && screenY < TopMargin + plotHeight)
            {
                _cellsToFill.Add((x, screenY));
            }
        }

        // Render all cells
        Driver.SetAttribute(colorAttr);
        foreach (var cell in _cellsToFill)
        {
            Move(cell.x, cell.y);

            if (isForeground)
            {
                // For foreground (current frame), use full blocks for solid appearance
                AddRune((Rune)'█');
            }
            else
            {
                // For background frames, use the depth-specific glyph
                AddRune((Rune)glyph);
            }
        }

        // For the foreground frame, add glow effect on leading edge
        if (isForeground && !_isPaused)
        {
            // Find the rightmost points and make them glow
            int maxX = int.MinValue;
            foreach (var cell in _cellsToFill)
            {
                if (cell.x > maxX) maxX = cell.x;
            }

            if (maxX > int.MinValue)
            {
                Driver.SetAttribute(_glowAttr);
                foreach (var cell in _cellsToFill.Where(c => c.x >= maxX - 2))
                {
                    Move(cell.x, cell.y);
                    AddRune((Rune)'█');
                }
            }
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

        // 3D mode controls
        Driver.SetAttribute(_dimAttr);
        AddStr("  [D]");
        Driver.SetAttribute(_3dEnabled ? _statusActiveAttr : _dimAttr);
        AddStr("3D");

        if (_3dEnabled)
        {
            Driver.SetAttribute(_dimAttr);
            AddStr("  [/]");
            Driver.SetAttribute(_statusActiveAttr);
            AddStr($"Depth:{_depthCount,2}");
        }

        // Fill rest with spaces and close border
        int currentPos = 1 + keys.Length + 5 + 7 + 5 + 5 + 4 + 5 + 2 + (_3dEnabled ? 7 + 8 : 0);
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
