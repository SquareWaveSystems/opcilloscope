using Terminal.Gui;
using Opcilloscope.OpcUa;
using Opcilloscope.OpcUa.Models;
using Opcilloscope.App.Themes;
using Opcilloscope.Utilities;
using System.Drawing;
using Attribute = Terminal.Gui.Attribute;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Views;

/// <summary>
/// Real-time Scope view supporting multiple signals with time-based x-axis.
/// Supports up to 5 simultaneous signals with distinct colors.
/// </summary>
public class ScopeView : View
{
    // === Theme tracking ===
    private AppTheme _currentTheme = null!;
    private readonly object _themeLock = new();

    // Sample with timestamp
    private record TimestampedSample(DateTime Timestamp, float Value);

    // Series data per node (up to 5)
    private class SeriesData
    {
        public MonitoredNode Node { get; init; } = null!;
        public List<TimestampedSample> Samples { get; } = new(200);
        public Terminal.Gui.Color LineColor { get; init; }
        public float VisibleMin { get; set; } = float.MaxValue;
        public float VisibleMax { get; set; } = float.MinValue;
    }

    private readonly List<SeriesData> _series = new();
    private readonly object _lock = new();
    private const int MaxSamples = 500;
    private const double DefaultTimeWindowSeconds = 30.0;

    // Distinct colors for up to 5 series (using Terminal.Gui.Color)
    private static readonly Terminal.Gui.Color[] SeriesColors =
    {
        Terminal.Gui.Color.Green,
        Terminal.Gui.Color.Cyan,
        Terminal.Gui.Color.Yellow,
        Terminal.Gui.Color.Magenta,
        Terminal.Gui.Color.White
    };

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
    private readonly double _timeWindowSeconds = DefaultTimeWindowSeconds;

    // GraphView and annotations
    private readonly GraphView _graphView;
    private readonly Label _headerLabel;
    private readonly Label _legendLabel;
    private readonly Label _statusLabel;

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

        // Header label showing signal names
        _headerLabel = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            TextAlignment = Alignment.Center
        };

        // Legend label showing series colors
        _legendLabel = new Label
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = 1,
            TextAlignment = Alignment.Center
        };

        // Create the GraphView
        _graphView = new GraphView
        {
            X = 0,
            Y = 2,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            BorderStyle = LineStyle.Single,
            CanFocus = false
        };

        // Configure axes
        _graphView.AxisX.Increment = 10;
        _graphView.AxisX.ShowLabelsEvery = 1;
        _graphView.AxisX.Text = "Time (s)";
        _graphView.AxisX.Minimum = 0;

        _graphView.AxisY.Increment = 10;
        _graphView.AxisY.ShowLabelsEvery = 1;
        _graphView.AxisY.Text = "Value";

        // Set margins for axis labels
        _graphView.MarginLeft = 10;
        _graphView.MarginBottom = 2;

        // Status bar at bottom
        _statusLabel = new Label
        {
            X = 0,
            Y = Pos.Bottom(_graphView),
            Width = Dim.Fill(),
            Height = 2,
            TextAlignment = Alignment.Center
        };

        Add(_headerLabel, _legendLabel, _graphView, _statusLabel);

        // Apply initial theme
        ApplyTheme();

        _startTime = DateTime.Now;
    }

    private void ApplyTheme()
    {
        AppTheme theme;
        lock (_themeLock)
        {
            theme = _currentTheme;
        }

        var normalAttr = theme.NormalAttr;
        var brightAttr = theme.BrightAttr;
        var dimAttr = theme.DimAttr;
        var accentAttr = theme.AccentAttr;

        ColorScheme = new ColorScheme
        {
            Normal = normalAttr,
            Focus = brightAttr,
            HotNormal = accentAttr,
            HotFocus = brightAttr,
            Disabled = dimAttr
        };

        _headerLabel.ColorScheme = new ColorScheme
        {
            Normal = brightAttr,
            Focus = brightAttr,
            HotNormal = brightAttr,
            HotFocus = brightAttr,
            Disabled = dimAttr
        };

        _legendLabel.ColorScheme = new ColorScheme
        {
            Normal = dimAttr,
            Focus = dimAttr,
            HotNormal = dimAttr,
            HotFocus = dimAttr,
            Disabled = dimAttr
        };

        _statusLabel.ColorScheme = new ColorScheme
        {
            Normal = dimAttr,
            Focus = dimAttr,
            HotNormal = dimAttr,
            HotFocus = dimAttr,
            Disabled = dimAttr
        };

        // Configure GraphView appearance
        _graphView.ColorScheme = new ColorScheme
        {
            Normal = normalAttr,
            Focus = brightAttr,
            HotNormal = accentAttr,
            HotFocus = brightAttr,
            Disabled = dimAttr
        };

        _graphView.BorderStyle = theme.BorderLineStyle;
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
                }

                _series.Add(series);
            }
        }

        // Subscribe to value changes from subscription manager
        _valueChangedHandler = OnValueChanged;
        _subscriptionManager.ValueChanged += _valueChangedHandler;

        // Start the refresh timer
        StartUpdateTimer();

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
                series.VisibleMin = float.MaxValue;
                series.VisibleMax = float.MinValue;
            }
        }
        _startTime = DateTime.Now;
        UpdateGraph();
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
        UpdateGraph();
        SetNeedsLayout();
        return true; // Keep timer running
    }

    /// <summary>
    /// Updates the GraphView with current sample data from all series.
    /// </summary>
    private void UpdateGraph()
    {
        AppTheme theme;
        lock (_themeLock)
        {
            theme = _currentTheme;
        }

        List<SeriesData> seriesCopy;
        lock (_lock)
        {
            seriesCopy = _series.ToList();
        }

        // Calculate global min/max for Y-axis across all series
        float globalMin = float.MaxValue;
        float globalMax = float.MinValue;
        double maxElapsedSeconds = 0;

        foreach (var series in seriesCopy)
        {
            lock (_lock)
            {
                foreach (var sample in series.Samples)
                {
                    if (sample.Value < globalMin) globalMin = sample.Value;
                    if (sample.Value > globalMax) globalMax = sample.Value;

                    var elapsed = (sample.Timestamp - _startTime).TotalSeconds;
                    if (elapsed > maxElapsedSeconds) maxElapsedSeconds = elapsed;
                }
            }
        }

        // If no samples were found, use default range
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

        // Ensure valid range
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

        // Update header with signal names
        string title = seriesCopy.Count > 0
            ? string.Join(" | ", seriesCopy.Select(s => s.Node.DisplayName?.ToUpperInvariant() ?? "?"))
            : "SCOPE";
        string statusIndicator = _isPaused ? theme.StatusHold : theme.StatusLive;
        string activityIndicator = !_isPaused && (_frameCount % 10) < 5 ? "●" : "○";

        _headerLabel.Text = $"{theme.TitleDecoration} {title} {theme.TitleDecoration}  {statusIndicator} {activityIndicator}";

        // Update legend with color indicators
        var legendParts = seriesCopy.Select((s, i) =>
            $"{GetColorName(SeriesColors[i])}:{s.Node.DisplayName ?? "?"}")
            .ToList();
        _legendLabel.Text = string.Join("  ", legendParts);

        // Update status bar
        string scaleInfo = _autoScale ? "AUTO" : $"{_scaleMultiplier:F1}X";
        int totalSamples = seriesCopy.Sum(s => s.Samples.Count);
        string timeInfo = FormatElapsedTime(maxElapsedSeconds);

        _statusLabel.Text = $"[SPACE] Pause  [+/-] Scale  [R] Auto    SCALE:{scaleInfo}  TIME:{timeInfo}  SAMPLES:{totalSamples}";

        // Clear previous annotations
        _graphView.Annotations.Clear();
        _graphView.Series.Clear();

        // Determine the time range for x-axis
        double timeRangeSeconds = Math.Max(maxElapsedSeconds, 10);

        if (seriesCopy.Any(s => s.Samples.Count > 0))
        {
            // Add path annotations for each series
            foreach (var series in seriesCopy)
            {
                List<TimestampedSample> samples;
                lock (_lock)
                {
                    samples = series.Samples.ToList();
                }

                if (samples.Count == 0) continue;

                var points = new List<PointF>();
                foreach (var sample in samples)
                {
                    var elapsedSeconds = (float)(sample.Timestamp - _startTime).TotalSeconds;
                    points.Add(new PointF(elapsedSeconds, sample.Value));
                }

                var path = new PathAnnotation
                {
                    Points = points,
                    LineColor = new Attribute(series.LineColor, theme.Background),
                    BeforeSeries = false
                };
                _graphView.Annotations.Add(path);
            }

            // Configure graph scaling
            float valueRange = visibleMax - visibleMin;
            if (valueRange < 1) valueRange = 1;

            int graphWidth = Math.Max(1, _graphView.Frame.Width - (int)_graphView.MarginLeft - 2);
            int graphHeight = Math.Max(1, _graphView.Frame.Height - (int)_graphView.MarginBottom - 2);

            float cellSizeX = (float)timeRangeSeconds / Math.Max(1, graphWidth);
            float cellSizeY = valueRange / Math.Max(1, graphHeight);

            _graphView.CellSize = new PointF(
                Math.Max(0.01f, cellSizeX),
                Math.Max(0.1f, cellSizeY)
            );

            _graphView.ScrollOffset = new PointF(0, visibleMin);

            // Configure axis labels
            _graphView.AxisX.Increment = (float)Math.Max(1, timeRangeSeconds / 5);
            _graphView.AxisX.ShowLabelsEvery = 1;
            _graphView.AxisX.Minimum = 0;
            _graphView.AxisX.LabelGetter = v => FormatTimeAxisLabel(v.Value);

            float yIncrement = valueRange / 4;
            _graphView.AxisY.Increment = yIncrement > 0 ? yIncrement : 10;
            _graphView.AxisY.ShowLabelsEvery = 1;
            _graphView.AxisY.LabelGetter = v => FormatAxisValue((float)v.Value);
        }
        else
        {
            // No data - show default range
            _graphView.CellSize = new PointF(1, 1);
            _graphView.ScrollOffset = new PointF(0, 0);
            _graphView.AxisX.Increment = 10;
            _graphView.AxisY.Increment = 10;

            // Add "No Signal" annotation
            var noSignalAnnotation = new TextAnnotation
            {
                Text = theme.NoSignalMessage,
                GraphPosition = new PointF(10, 50)
            };
            _graphView.Annotations.Add(noSignalAnnotation);
        }

        _graphView.SetNeedsLayout();
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

    private static string FormatTimeAxisLabel(double seconds) => FormatElapsedTime(seconds);

    /// <summary>
    /// Toggles pause state.
    /// </summary>
    public void TogglePause()
    {
        _isPaused = !_isPaused;
        PauseStateChanged?.Invoke(_isPaused);
        UpdateGraph();
        SetNeedsLayout();
    }

    /// <summary>
    /// Increases vertical scale (zoom in).
    /// </summary>
    public void IncreaseScale()
    {
        _autoScale = false;
        _scaleMultiplier *= 1.2f;
        UpdateGraph();
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
        UpdateGraph();
        SetNeedsLayout();
    }

    /// <summary>
    /// Resets to auto-scale mode.
    /// </summary>
    public void ResetScale()
    {
        _autoScale = true;
        _scaleMultiplier = 1.0f;
        UpdateGraph();
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
