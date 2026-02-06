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
/// Real-time 2D scrolling oscilloscope-style plot using Terminal.Gui GraphView.
/// Supports multiple themes via ThemeManager.
/// </summary>
public class TrendPlotView : View
{
    // === Theme tracking ===
    private AppTheme _currentTheme = null!;
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

    // GraphView and series
    private readonly GraphView _graphView;
    private PathAnnotation? _waveformPath;
    private readonly Label _headerLabel;
    private readonly Label _statusLabel;
    private readonly Label _infoLabel;

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
        }

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;

        CanFocus = true;
        WantMousePositionReports = false;

        // Header label showing signal name and status
        _headerLabel = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = 1,
            TextAlignment = Alignment.Center
        };

        // Info label showing scale and sample info
        _infoLabel = new Label
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
        _graphView.AxisX.Increment = 20;
        _graphView.AxisX.ShowLabelsEvery = 1;
        _graphView.AxisX.Text = "Samples";
        _graphView.AxisX.Minimum = 0;

        _graphView.AxisY.Increment = 10;
        _graphView.AxisY.ShowLabelsEvery = 1;
        _graphView.AxisY.Text = "Value";

        // Set margins for axis labels
        _graphView.MarginLeft = 8;
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

        Add(_headerLabel, _infoLabel, _graphView, _statusLabel);

        // Apply initial theme
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        AppTheme theme;
        lock (_themeLock)
        {
            theme = _currentTheme;
        }

        var bgAttr = new Attribute(theme.Background, theme.Background);
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

        _infoLabel.ColorScheme = new ColorScheme
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
        UpdateGraph();
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

        UpdateGraph();
        SetNeedsLayout();
        return true; // Keep timer running
    }

    /// <summary>
    /// Updates the GraphView with current sample data.
    /// </summary>
    private void UpdateGraph()
    {
        // Get samples to display
        float[] displaySamples;
        int displayCount;
        lock (_lock)
        {
            displayCount = _sampleCount;
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

        // Update header
        AppTheme theme;
        lock (_themeLock)
        {
            theme = _currentTheme;
        }

        string title = _boundNode?.DisplayName?.ToUpperInvariant() ?? "SCOPE";
        string statusIndicator = _isPaused ? theme.StatusHold : theme.StatusLive;
        string activityIndicator = !_isPaused && (_frameCount % 10) < 5 ? "●" : "○";

        _headerLabel.Text = $"{theme.TitleDecoration} {title} {theme.TitleDecoration}  {statusIndicator} {activityIndicator}";

        // Update info
        string scaleInfo = _autoScale ? "AUTO" : $"{_scaleMultiplier:F1}X";
        _infoLabel.Text = $"CH1  SCALE:{scaleInfo}  SAMPLES:{displayCount,4}  RANGE:[{FormatAxisValue(_visibleMin)},{FormatAxisValue(_visibleMax)}]";

        // Update status bar
        float currentValue = 0;
        lock (_lock)
        {
            if (_sampleCount > 0)
            {
                int lastIdx = (_writeIndex - 1 + _samples.Length) % _samples.Length;
                currentValue = _samples[lastIdx];
            }
        }

        _statusLabel.Text = $"SPACE Pause  +/- Scale  R Auto    VALUE: {currentValue:F2}";

        // Clear previous annotations
        _graphView.Annotations.Clear();
        _graphView.Series.Clear();

        if (displayCount > 0)
        {
            // Create points for the waveform
            var points = new List<PointF>();
            for (int i = 0; i < displayCount; i++)
            {
                points.Add(new PointF(i, displaySamples[i]));
            }

            // Get line color from theme
            var lineColor = theme.Accent;

            // Create path annotation for connected line
            _waveformPath = new PathAnnotation
            {
                Points = points,
                LineColor = new Attribute(lineColor, theme.Background),
                BeforeSeries = false
            };
            _graphView.Annotations.Add(_waveformPath);

            // Configure graph scaling
            float valueRange = _visibleMax - _visibleMin;
            if (valueRange < 1) valueRange = 1;

            // Calculate cell size based on available graph area and data range
            int graphWidth = Math.Max(1, _graphView.Frame.Width - (int)_graphView.MarginLeft - 2);
            int graphHeight = Math.Max(1, _graphView.Frame.Height - (int)_graphView.MarginBottom - 2);

            // CellSize determines how many graph units per cell
            float cellSizeX = (float)displayCount / Math.Max(1, graphWidth);
            float cellSizeY = valueRange / Math.Max(1, graphHeight);

            _graphView.CellSize = new PointF(
                Math.Max(0.1f, cellSizeX),
                Math.Max(0.1f, cellSizeY)
            );

            // Set scroll offset to show data from bottom-left
            _graphView.ScrollOffset = new PointF(0, _visibleMin);

            // Configure axis increments based on range
            _graphView.AxisX.Increment = Math.Max(1, displayCount / 5);
            _graphView.AxisX.ShowLabelsEvery = 1;
            _graphView.AxisX.Minimum = 0;

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
            _graphView.AxisX.Increment = 20;
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
