using Terminal.Gui;
using Opcilloscope.OpcUa;
using Opcilloscope.OpcUa.Models;
using Opcilloscope.App.Themes;
using Opcilloscope.Utilities;
using Attribute = Terminal.Gui.Attribute;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Views;

/// <summary>
/// Real-time Scope view supporting multiple signals with time-based x-axis.
/// Renders waveforms using braille Unicode characters for 8x resolution.
/// Supports up to 5 simultaneous signals with distinct colors.
/// </summary>
public class ScopeView : View
{
    // === Theme tracking ===
    private AppTheme _currentTheme = null!;
    private readonly object _themeLock = new();

    // Sample with timestamp
    private record TimestampedSample(DateTime Timestamp, float Value);

    // Series data per node (up to 5) with stats tracking
    private class SeriesData
    {
        public MonitoredNode Node { get; init; } = null!;
        public List<TimestampedSample> Samples { get; } = new(500);
        public Terminal.Gui.Color LineColor { get; init; }

        // Stats tracking
        public float CurrentValue { get; set; } = float.NaN;
        public float MinValue { get; set; } = float.MaxValue;
        public float MaxValue { get; set; } = float.MinValue;
        public double Sum { get; set; }
        public int Count { get; set; }
        public float Average => Count > 0 ? (float)(Sum / Count) : float.NaN;
    }

    private readonly List<SeriesData> _series = new();
    private readonly object _lock = new();
    private const int MaxSamples = 500;
    private const double DefaultTimeWindowSeconds = 30.0;
    private const double MinTimeWindowSeconds = 5.0;
    private const double MaxTimeWindowSeconds = 300.0;
    private const double TimeWindowZoomFactor = 1.5;

    // Distinct colors for up to 5 series
    private static readonly Terminal.Gui.Color[] SeriesColors =
    {
        Terminal.Gui.Color.Green,
        Terminal.Gui.Color.Cyan,
        Terminal.Gui.Color.Yellow,
        Terminal.Gui.Color.Magenta,
        Terminal.Gui.Color.White
    };

    // Layout constants
    private const int HeaderRows = 2;    // Title + legend
    private const int StatusRows = 2;    // Status bar
    private const int YAxisLabelWidth = 9; // Left margin for Y-axis labels
    private const int XAxisLabelHeight = 1; // Bottom margin for X-axis labels
    private const int GridDotSpacing = 4;  // Braille-pixel spacing for grid dots

    // Auto-scale tracking
    private bool _autoScale = true;
    private float _scaleMultiplier = 1.0f;

    // State
    private bool _isPaused;
    private SubscriptionManager? _subscriptionManager;
    private Action<MonitoredNode>? _valueChangedHandler;
    private object? _timerToken;
    private int _frameCount;
    private DateTime _startTime;
    private double _timeWindowSeconds = DefaultTimeWindowSeconds;

    // Cursor state (active only when paused)
    private bool _cursorActive;
    private int _cursorPixelX; // cursor position in braille pixel coordinates

    /// <summary>
    /// Event fired when pause state changes.
    /// </summary>
    public event Action<bool>? PauseStateChanged;

    /// <summary>
    /// Gets whether the plot is currently paused.
    /// </summary>
    public bool IsPaused => _isPaused;

    public ScopeView()
    {
        // Initialize cached theme
        lock (_themeLock)
        {
            _currentTheme = ThemeManager.Current;
        }

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;

        CanFocus = true;
        WantMousePositionReports = false;

        _startTime = DateTime.Now;
    }

    private void ApplyTheme()
    {
        AppTheme theme;
        lock (_themeLock)
        {
            theme = _currentTheme;
        }

        ColorScheme = new ColorScheme
        {
            Normal = theme.NormalAttr,
            Focus = theme.BrightAttr,
            HotNormal = theme.AccentAttr,
            HotFocus = theme.BrightAttr,
            Disabled = theme.DimAttr
        };
    }

    private void OnThemeChanged(AppTheme newTheme)
    {
        lock (_themeLock)
        {
            _currentTheme = newTheme;
        }

        try
        {
            Application.Invoke(() =>
            {
                ApplyTheme();
                SetNeedsLayout();
            });
        }
        catch (InvalidOperationException)
        {
            // Application may not be initialized yet - ignore
        }
    }

    /// <summary>
    /// Binds to multiple monitored nodes to display their values as separate series.
    /// </summary>
    public void BindToNodes(IReadOnlyList<MonitoredNode> nodes, SubscriptionManager subscriptionManager)
    {
        UnbindCurrentNodes();

        _subscriptionManager = subscriptionManager;
        _startTime = DateTime.Now;

        lock (_lock)
        {
            _series.Clear();

            for (int i = 0; i < nodes.Count && i < SeriesColors.Length; i++)
            {
                var node = nodes[i];
                var series = new SeriesData
                {
                    Node = node,
                    LineColor = SeriesColors[i]
                };

                // Try to parse current value as initial sample
                if (TryParseValue(node.Value, out var value))
                {
                    series.Samples.Add(new TimestampedSample(DateTime.Now, value));
                    series.CurrentValue = value;
                    series.MinValue = value;
                    series.MaxValue = value;
                    series.Sum = value;
                    series.Count = 1;
                }

                _series.Add(series);
            }
        }

        // Subscribe to value changes from subscription manager
        _valueChangedHandler = OnValueChanged;
        _subscriptionManager.ValueChanged += _valueChangedHandler;

        // Start the refresh timer
        StartUpdateTimer();

        ApplyTheme();
        SetNeedsLayout();
    }

    private void OnValueChanged(MonitoredNode node)
    {
        if (_isPaused) return;

        lock (_lock)
        {
            var series = _series.FirstOrDefault(s => s.Node.ClientHandle == node.ClientHandle);
            if (series != null && TryParseValue(node.Value, out var value))
            {
                var sample = new TimestampedSample(DateTime.Now, value);
                series.Samples.Add(sample);

                // Update stats
                series.CurrentValue = value;
                if (value < series.MinValue) series.MinValue = value;
                if (value > series.MaxValue) series.MaxValue = value;
                series.Sum += value;
                series.Count++;

                // Trim old samples beyond time window
                var cutoff = DateTime.Now.AddSeconds(-_timeWindowSeconds);
                var removeCount = 0;
                while (removeCount < series.Samples.Count && series.Samples[removeCount].Timestamp < cutoff)
                {
                    removeCount++;
                }
                if (removeCount > 0)
                {
                    series.Samples.RemoveRange(0, removeCount);
                }

                // Also enforce max samples limit
                if (series.Samples.Count > MaxSamples)
                {
                    var excess = series.Samples.Count - MaxSamples;
                    series.Samples.RemoveRange(0, excess);
                }
            }
        }
    }

    private void UnbindCurrentNodes()
    {
        StopUpdateTimer();
        if (_valueChangedHandler != null && _subscriptionManager != null)
        {
            _subscriptionManager.ValueChanged -= _valueChangedHandler;
        }
        _valueChangedHandler = null;
        _subscriptionManager = null;

        lock (_lock)
        {
            _series.Clear();
        }
    }

    /// <summary>
    /// Clears all samples and resets the view.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            foreach (var series in _series)
            {
                series.Samples.Clear();
                series.CurrentValue = float.NaN;
                series.MinValue = float.MaxValue;
                series.MaxValue = float.MinValue;
                series.Sum = 0;
                series.Count = 0;
            }
        }
        _startTime = DateTime.Now;
        _cursorActive = false;
        SetNeedsLayout();
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
        SetNeedsLayout();
        return true; // Keep timer running
    }

    /// <summary>
    /// Renders the entire scope view using braille characters.
    /// </summary>
    protected override bool OnDrawingContent(DrawContext? context)
    {
        if (Driver is null) return false;

        AppTheme theme;
        lock (_themeLock)
        {
            theme = _currentTheme;
        }

        List<SeriesData> seriesCopy;
        List<List<TimestampedSample>> samplesCopy;
        lock (_lock)
        {
            seriesCopy = _series.ToList();
            samplesCopy = _series.Select(s => s.Samples.ToList()).ToList();
        }

        var viewport = Viewport;
        int totalWidth = viewport.Width;
        int totalHeight = viewport.Height;

        if (totalWidth < 5 || totalHeight < 5) return false;

        // Layout regions
        int plotLeft = YAxisLabelWidth;
        int plotTop = HeaderRows;
        int plotWidth = totalWidth - plotLeft;
        int plotHeight = totalHeight - HeaderRows - StatusRows - XAxisLabelHeight;

        if (plotWidth < 2 || plotHeight < 2) return false;

        // Clear the viewport
        var normalAttr = theme.NormalAttr;
        Driver!.SetAttribute(normalAttr);
        for (int y = 0; y < totalHeight; y++)
        {
            Move(0, y);
            for (int x = 0; x < totalWidth; x++)
                Driver!.AddRune(' ');
        }

        // === Draw header ===
        DrawHeader(theme, seriesCopy, totalWidth);

        // === Calculate data ranges ===
        var now = _isPaused && samplesCopy.Any(s => s.Count > 0)
            ? samplesCopy.Where(s => s.Count > 0).Max(s => s.Last().Timestamp)
            : DateTime.Now;
        var windowStart = now.AddSeconds(-_timeWindowSeconds);
        var windowDuration = _timeWindowSeconds;

        float globalMin = float.MaxValue;
        float globalMax = float.MinValue;

        for (int i = 0; i < samplesCopy.Count; i++)
        {
            foreach (var sample in samplesCopy[i])
            {
                if (sample.Timestamp >= windowStart)
                {
                    if (sample.Value < globalMin) globalMin = sample.Value;
                    if (sample.Value > globalMax) globalMax = sample.Value;
                }
            }
        }

        if (globalMin == float.MaxValue || globalMax == float.MinValue)
        {
            globalMin = 0;
            globalMax = 100;
        }

        // Apply auto-scale or manual scale
        float visibleMin, visibleMax;
        if (_autoScale)
        {
            visibleMin = globalMin;
            visibleMax = globalMax;
        }
        else
        {
            float center = (globalMin + globalMax) / 2;
            float range = (globalMax - globalMin) / 2;
            range = Math.Max(range, 1f) / _scaleMultiplier;
            visibleMin = center - range;
            visibleMax = center + range;
        }

        if (visibleMax <= visibleMin)
        {
            visibleMin = globalMin - 1;
            visibleMax = globalMax + 1;
        }

        // Add padding
        float padding = (visibleMax - visibleMin) * 0.1f;
        if (padding < 0.1f) padding = 0.1f;
        visibleMin -= padding;
        visibleMax += padding;

        // === Create braille canvas and render ===
        var canvas = new BrailleCanvas(plotWidth, plotHeight);

        int canvasPixelW = canvas.PixelWidth;
        int canvasPixelH = canvas.PixelHeight;

        // Draw grid lines (layer -1)
        DrawGrid(canvas, canvasPixelW, canvasPixelH);

        // Draw each series as connected line segments
        bool hasSamples = false;
        for (int si = 0; si < samplesCopy.Count; si++)
        {
            var samples = samplesCopy[si];
            if (samples.Count == 0) continue;
            hasSamples = true;

            int prevPx = -1, prevPy = -1;
            foreach (var sample in samples)
            {
                if (sample.Timestamp < windowStart) continue;

                double tx = (sample.Timestamp - windowStart).TotalSeconds / windowDuration;
                double ty = 1.0 - (sample.Value - visibleMin) / (visibleMax - visibleMin);

                int px = (int)Math.Round(tx * (canvasPixelW - 1));
                int py = (int)Math.Round(ty * (canvasPixelH - 1));

                px = Math.Clamp(px, 0, canvasPixelW - 1);
                py = Math.Clamp(py, 0, canvasPixelH - 1);

                if (prevPx >= 0)
                {
                    canvas.DrawLine(prevPx, prevPy, px, py, layer: si);
                }
                else
                {
                    canvas.SetPixel(px, py, layer: si);
                }

                prevPx = px;
                prevPy = py;
            }
        }

        // Draw cursor vertical line (highest priority layer = 100) if active
        if (_cursorActive && _isPaused)
        {
            int cursorX = Math.Clamp(_cursorPixelX, 0, canvasPixelW - 1);
            for (int py = 0; py < canvasPixelH; py++)
            {
                canvas.SetPixel(cursorX, py, layer: 100);
            }
        }

        // === Render canvas to terminal ===
        var gridAttr = theme.GridAttr;
        var accentAttr = theme.AccentAttr;

        // Pre-build signal attributes
        var signalAttrs = new Attribute[SeriesColors.Length];
        for (int i = 0; i < SeriesColors.Length; i++)
        {
            signalAttrs[i] = new Attribute(SeriesColors[i], theme.Background);
        }

        for (int cy = 0; cy < canvas.CellHeight; cy++)
        {
            Move(plotLeft, plotTop + cy);
            for (int cx = 0; cx < canvas.CellWidth; cx++)
            {
                char brailleChar = canvas.GetCellFiltered(cx, cy);
                if (brailleChar == '\u2800')
                {
                    Driver!.SetAttribute(normalAttr);
                    Driver!.AddRune(' ');
                    continue;
                }

                int dominantLayer = canvas.GetCellDominantLayer(cx, cy);

                if (dominantLayer == 100)
                {
                    // Cursor
                    Driver!.SetAttribute(accentAttr);
                }
                else if (dominantLayer >= 0 && dominantLayer < signalAttrs.Length)
                {
                    Driver!.SetAttribute(signalAttrs[dominantLayer]);
                }
                else
                {
                    // Grid or unknown
                    Driver!.SetAttribute(gridAttr);
                }

                Driver!.AddRune(brailleChar);
            }
        }

        // === Draw Y-axis labels ===
        DrawYAxisLabels(theme, plotTop, plotHeight, visibleMin, visibleMax);

        // === Draw X-axis labels ===
        DrawXAxisLabels(theme, plotLeft, plotTop + plotHeight, plotWidth, windowDuration);

        // === Draw stats overlay ===
        if (hasSamples)
        {
            DrawStatsOverlay(theme, seriesCopy, plotLeft, plotTop, plotWidth);
        }
        else
        {
            // No signal message
            var msg = theme.NoSignalMessage;
            int msgX = plotLeft + (plotWidth - msg.Length) / 2;
            int msgY = plotTop + plotHeight / 2;
            if (msgX >= 0 && msgY >= 0)
            {
                Driver!.SetAttribute(theme.DimAttr);
                Move(msgX, msgY);
                Driver!.AddStr(msg);
            }
        }

        // === Draw status bar ===
        DrawStatusBar(theme, seriesCopy, samplesCopy, totalWidth, totalHeight,
                      windowStart, windowDuration, canvasPixelW);

        return true;
    }

    private void DrawHeader(AppTheme theme, List<SeriesData> seriesCopy, int totalWidth)
    {
        // Title line
        string title = seriesCopy.Count > 0
            ? string.Join(" | ", seriesCopy.Select(s => s.Node.DisplayName?.ToUpperInvariant() ?? "?"))
            : "SCOPE";
        string statusIndicator = _isPaused ? theme.StatusHold : theme.StatusLive;
        string activityIndicator = !_isPaused && (_frameCount % 10) < 5 ? "●" : "○";
        string headerText = $"{theme.TitleDecoration} {title} {theme.TitleDecoration}  {statusIndicator} {activityIndicator}";

        Driver!.SetAttribute(theme.BrightAttr);
        int headerX = Math.Max(0, (totalWidth - headerText.Length) / 2);
        Move(headerX, 0);
        Driver!.AddStr(headerText.Length <= totalWidth ? headerText : headerText[..totalWidth]);

        // Legend line
        var legendParts = seriesCopy.Select((s, i) =>
            $"{GetColorName(SeriesColors[i])}:{s.Node.DisplayName ?? "?"}")
            .ToList();

        string legendText = string.Join("  ", legendParts);
        Driver!.SetAttribute(theme.DimAttr);
        int legendX = Math.Max(0, (totalWidth - legendText.Length) / 2);
        Move(legendX, 1);
        Driver!.AddStr(legendText.Length <= totalWidth ? legendText : legendText[..totalWidth]);
    }

    private void DrawGrid(BrailleCanvas canvas, int pixelW, int pixelH)
    {
        // Horizontal grid lines at roughly 5 positions
        int numHLines = 4;
        for (int i = 1; i < numHLines; i++)
        {
            int y = i * pixelH / numHLines;
            canvas.DrawDottedHorizontalLine(y, GridDotSpacing);
        }

        // Vertical grid lines at roughly 6 positions
        int numVLines = 5;
        for (int i = 1; i < numVLines; i++)
        {
            int x = i * pixelW / numVLines;
            canvas.DrawDottedVerticalLine(x, GridDotSpacing);
        }
    }

    private void DrawYAxisLabels(AppTheme theme, int plotTop, int plotHeight,
                                  float visibleMin, float visibleMax)
    {
        Driver!.SetAttribute(theme.DimAttr);

        int numLabels = Math.Min(plotHeight, 5);
        for (int i = 0; i <= numLabels; i++)
        {
            float fraction = (float)i / numLabels;
            float value = visibleMax - fraction * (visibleMax - visibleMin);
            string label = FormatAxisValue(value);

            int y = plotTop + (int)(fraction * (plotHeight - 1));
            // Right-align the label
            int x = Math.Max(0, YAxisLabelWidth - 1 - label.Length);
            Move(x, y);
            Driver!.AddStr(label);
        }
    }

    private void DrawXAxisLabels(AppTheme theme, int plotLeft, int labelY,
                                  int plotWidth, double windowDuration)
    {
        Driver!.SetAttribute(theme.DimAttr);

        int numLabels = Math.Min(plotWidth / 8, 6); // At least 8 chars apart
        if (numLabels < 2) numLabels = 2;

        for (int i = 0; i <= numLabels; i++)
        {
            double fraction = (double)i / numLabels;
            double seconds = fraction * windowDuration;
            string label = FormatElapsedTime(seconds);

            int x = plotLeft + (int)(fraction * (plotWidth - 1));
            // Center the label around x
            int labelX = x - label.Length / 2;
            labelX = Math.Clamp(labelX, plotLeft, plotLeft + plotWidth - label.Length);

            Move(labelX, labelY);
            Driver!.AddStr(label);
        }
    }

    private void DrawStatsOverlay(AppTheme theme, List<SeriesData> seriesCopy,
                                   int plotLeft, int plotTop, int plotWidth)
    {
        // Draw stats in top-right corner of the plot area
        for (int i = 0; i < seriesCopy.Count; i++)
        {
            var s = seriesCopy[i];
            string colorName = GetColorName(s.LineColor);

            string statsText;
            if (float.IsNaN(s.CurrentValue))
            {
                statsText = $"{colorName}: ---";
            }
            else
            {
                statsText = $"{colorName}: {FormatAxisValue(s.CurrentValue)}" +
                           $" [{FormatAxisValue(s.MinValue)}/{FormatAxisValue(s.MaxValue)}" +
                           $" avg:{FormatAxisValue(s.Average)}]";
            }

            int x = plotLeft + plotWidth - statsText.Length - 1;
            if (x < plotLeft) x = plotLeft;
            int y = plotTop + i;

            // Use signal color for the stat line
            var attr = new Attribute(s.LineColor, theme.Background);
            Driver!.SetAttribute(attr);
            Move(x, y);
            Driver!.AddStr(statsText.Length <= plotWidth ? statsText : statsText[..plotWidth]);
        }
    }

    private void DrawStatusBar(AppTheme theme, List<SeriesData> seriesCopy,
                                List<List<TimestampedSample>> samplesCopy,
                                int totalWidth, int totalHeight,
                                DateTime windowStart, double windowDuration,
                                int canvasPixelW)
    {
        int statusY = totalHeight - StatusRows;
        Driver!.SetAttribute(theme.DimAttr);

        string scaleInfo = _autoScale ? "AUTO" : $"{_scaleMultiplier:F1}X";
        int totalSamples = samplesCopy.Sum(s => s.Count);
        double elapsed = (DateTime.Now - _startTime).TotalSeconds;
        string timeInfo = FormatElapsedTime(elapsed);
        string windowInfo = FormatElapsedTime(_timeWindowSeconds);

        string statusText;
        if (_isPaused && _cursorActive)
        {
            // Cursor mode status
            double cursorFraction = canvasPixelW > 1
                ? (double)_cursorPixelX / (canvasPixelW - 1)
                : 0;
            double cursorSeconds = cursorFraction * windowDuration;
            string cursorTime = $"{cursorSeconds:F1}s";

            var cursorValues = new List<string>();
            for (int i = 0; i < seriesCopy.Count && i < samplesCopy.Count; i++)
            {
                var samples = samplesCopy[i];
                string colorName = GetColorName(seriesCopy[i].LineColor);
                float interpolated = InterpolateSampleAtTime(samples, windowStart, windowDuration, cursorFraction);
                cursorValues.Add($"{colorName}:{FormatAxisValue(interpolated)}");
            }

            statusText = $"<LEFT/RIGHT> Cursor  <SPACE> Resume  CURSOR @ {cursorTime}  {string.Join("  ", cursorValues)}";
        }
        else
        {
            statusText = $"<SPACE> Pause  <+/-> VScale  <R> Auto  <[/]> Time" +
                         $"  SCALE:{scaleInfo}  WIN:{windowInfo}  TIME:{timeInfo}  SAMPLES:{totalSamples}";
        }

        int statusX = Math.Max(0, (totalWidth - statusText.Length) / 2);
        Move(statusX, statusY);
        Driver!.AddStr(statusText.Length <= totalWidth ? statusText : statusText[..totalWidth]);
    }

    /// <summary>
    /// Interpolates a value from sample data at a given fractional position within the time window.
    /// </summary>
    private static float InterpolateSampleAtTime(List<TimestampedSample> samples,
                                                  DateTime windowStart, double windowDuration,
                                                  double fraction)
    {
        if (samples.Count == 0) return float.NaN;

        var targetTime = windowStart.AddSeconds(fraction * windowDuration);

        // Find the two samples surrounding the target time
        int idx = samples.FindIndex(s => s.Timestamp >= targetTime);

        if (idx < 0) return samples[^1].Value; // Past the end
        if (idx == 0) return samples[0].Value;  // Before the start

        var before = samples[idx - 1];
        var after = samples[idx];

        double totalSpan = (after.Timestamp - before.Timestamp).TotalSeconds;
        if (totalSpan <= 0) return before.Value;

        double t = (targetTime - before.Timestamp).TotalSeconds / totalSpan;
        return (float)(before.Value + t * (after.Value - before.Value));
    }

    private static string GetColorName(Terminal.Gui.Color color)
    {
        if (color == Terminal.Gui.Color.Green) return "GRN";
        if (color == Terminal.Gui.Color.Cyan) return "CYN";
        if (color == Terminal.Gui.Color.Yellow) return "YEL";
        if (color == Terminal.Gui.Color.Magenta) return "MAG";
        if (color == Terminal.Gui.Color.White) return "WHT";
        return "???";
    }

    private static string FormatElapsedTime(double seconds)
    {
        if (seconds < 60)
        {
            return $"{seconds:F0}s";
        }
        else
        {
            var mins = (int)(seconds / 60);
            var secs = (int)(seconds % 60);
            return $"{mins}:{secs:D2}";
        }
    }

    /// <summary>
    /// Toggles pause state.
    /// </summary>
    public void TogglePause()
    {
        _isPaused = !_isPaused;
        if (!_isPaused)
        {
            // Resuming - hide cursor
            _cursorActive = false;
        }
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
    /// Widens the time window (show more history).
    /// </summary>
    public void WidenTimeWindow()
    {
        _timeWindowSeconds = Math.Min(_timeWindowSeconds * TimeWindowZoomFactor, MaxTimeWindowSeconds);
        SetNeedsLayout();
    }

    /// <summary>
    /// Narrows the time window (zoom in on time).
    /// </summary>
    public void NarrowTimeWindow()
    {
        _timeWindowSeconds = Math.Max(_timeWindowSeconds / TimeWindowZoomFactor, MinTimeWindowSeconds);
        SetNeedsLayout();
    }

    /// <summary>
    /// Moves the cursor left (when paused).
    /// </summary>
    public void MoveCursorLeft()
    {
        if (!_isPaused) return;
        _cursorActive = true;
        _cursorPixelX = Math.Max(0, _cursorPixelX - 2);
        SetNeedsLayout();
    }

    /// <summary>
    /// Moves the cursor right (when paused).
    /// </summary>
    public void MoveCursorRight()
    {
        if (!_isPaused) return;
        _cursorActive = true;

        // Determine max from current plot width
        int plotWidth = Math.Max(1, Frame.Width - YAxisLabelWidth);
        int maxPixelX = plotWidth * 2 - 1;
        _cursorPixelX = Math.Min(maxPixelX, _cursorPixelX + 2);
        SetNeedsLayout();
    }

    protected override bool OnKeyDown(Key key)
    {
        switch (key.KeyCode)
        {
            case KeyCode.Space:
                TogglePause();
                return true;

            case KeyCode.D0 when key.IsShift: // + key
            case (KeyCode)'=':
            case (KeyCode)'+':
                IncreaseScale();
                return true;

            case KeyCode.D9 when key.IsShift: // ( key
            case (KeyCode)'-':
                DecreaseScale();
                return true;

            case (KeyCode)'r':
            case (KeyCode)'R':
                ResetScale();
                return true;

            case (KeyCode)'[':
                WidenTimeWindow();
                return true;

            case (KeyCode)']':
                NarrowTimeWindow();
                return true;

            case KeyCode.CursorLeft:
                MoveCursorLeft();
                return true;

            case KeyCode.CursorRight:
                MoveCursorRight();
                return true;
        }

        return base.OnKeyDown(key);
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
            UnbindCurrentNodes();
        }
        base.Dispose(disposing);
    }
}
