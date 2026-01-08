using Terminal.Gui;
using Opcilloscope.App.Views;
using Opcilloscope.App.Themes;
using Opcilloscope.OpcUa;
using Opcilloscope.OpcUa.Models;
using ThemeManager = Opcilloscope.App.Themes.ThemeManager;

namespace Opcilloscope.App.Dialogs;

/// <summary>
/// Simplified dialog for displaying a real-time Scope view with multiple signals.
/// No node selection or demo mode - nodes are pre-selected before launch.
/// </summary>
public class ScopeDialog : Dialog
{
    private readonly ScopeView _scopeView;
    private readonly Button _closeButton;
    private readonly Button _pauseButton;
    private readonly Button _aliensModeButton;

    // Theme-aware accessor
    private AppTheme Theme => ThemeManager.Current;

    public ScopeDialog(
        IReadOnlyList<MonitoredNode> selectedNodes,
        SubscriptionManager subscriptionManager)
    {
        Title = $"{Theme.TitleDecoration}[ SCOPE ]{Theme.TitleDecoration}";
        Width = Dim.Percent(90);
        Height = Dim.Percent(90);

        // Apply theme-based styling
        ColorScheme = Theme.DialogColorScheme;
        BorderStyle = Theme.BorderLineStyle;

        // Create the scope view - takes up most of the dialog
        _scopeView = new ScopeView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(2)
        };

        // Simple button row with just pause and close
        var buttonFrame = new View
        {
            X = 0,
            Y = Pos.Bottom(_scopeView),
            Width = Dim.Fill(),
            Height = 2,
            ColorScheme = ColorScheme
        };

        _pauseButton = new Button
        {
            X = 1,
            Y = 0,
            Text = $"{Theme.ButtonPrefix}PAUSE{Theme.ButtonSuffix}",
            ColorScheme = Theme.ButtonColorScheme
        };
        _pauseButton.Accepting += OnPauseToggle;

        _aliensModeButton = new Button
        {
            X = Pos.Right(_pauseButton) + 2,
            Y = 0,
            Text = $"{Theme.ButtonPrefix}ALIENS{Theme.ButtonSuffix}",
            ColorScheme = Theme.ButtonColorScheme
        };
        _aliensModeButton.Accepting += OnAliensModeToggle;

        _closeButton = new Button
        {
            X = Pos.Right(_aliensModeButton) + 2,
            Y = 0,
            Text = $"{Theme.ButtonPrefix}CLOSE{Theme.ButtonSuffix}",
            ColorScheme = Theme.ButtonColorScheme
        };
        _closeButton.Accepting += (_, _) => Application.RequestStop();

        buttonFrame.Add(_pauseButton, _aliensModeButton, _closeButton);

        Add(_scopeView, buttonFrame);

        // Subscribe to theme changes
        ThemeManager.ThemeChanged += OnThemeChanged;

        // Subscribe to pause state changes to update button text
        _scopeView.PauseStateChanged += OnPauseStateChanged;

        // Subscribe to aliens mode state changes to update button text
        _scopeView.AliensModeChanged += OnAliensModeStateChanged;

        // Bind to all selected nodes
        _scopeView.BindToNodes(selectedNodes, subscriptionManager);

        _scopeView.SetFocus();
    }

    private void OnPauseToggle(object? _, CommandEventArgs _1)
    {
        _scopeView.TogglePause();
    }

    private void OnPauseStateChanged(bool isPaused)
    {
        Application.Invoke(() =>
        {
            _pauseButton.Text = isPaused
                ? $"{Theme.ButtonPrefix}RESUME{Theme.ButtonSuffix}"
                : $"{Theme.ButtonPrefix}PAUSE{Theme.ButtonSuffix}";
        });
    }

    private void OnAliensModeToggle(object? _, CommandEventArgs _1)
    {
        _scopeView.ToggleAliensMode();
    }

    private void OnAliensModeStateChanged(bool isAliensMode)
    {
        Application.Invoke(() =>
        {
            _aliensModeButton.Text = isAliensMode
                ? $"{Theme.ButtonPrefix}NORMAL{Theme.ButtonSuffix}"
                : $"{Theme.ButtonPrefix}ALIENS{Theme.ButtonSuffix}";
        });
    }

    private void OnThemeChanged(AppTheme theme)
    {
        Application.Invoke(() =>
        {
            Title = $"{theme.TitleDecoration}[ SCOPE ]{theme.TitleDecoration}";
            ColorScheme = theme.DialogColorScheme;
            BorderStyle = theme.BorderLineStyle;

            _pauseButton.Text = _scopeView.IsPaused
                ? $"{theme.ButtonPrefix}RESUME{theme.ButtonSuffix}"
                : $"{theme.ButtonPrefix}PAUSE{theme.ButtonSuffix}";
            _pauseButton.ColorScheme = theme.ButtonColorScheme;

            _aliensModeButton.Text = _scopeView.IsAliensMode
                ? $"{theme.ButtonPrefix}NORMAL{theme.ButtonSuffix}"
                : $"{theme.ButtonPrefix}ALIENS{theme.ButtonSuffix}";
            _aliensModeButton.ColorScheme = theme.ButtonColorScheme;

            _closeButton.Text = $"{theme.ButtonPrefix}CLOSE{theme.ButtonSuffix}";
            _closeButton.ColorScheme = theme.ButtonColorScheme;

            _scopeView.SetNeedsLayout();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            _scopeView.PauseStateChanged -= OnPauseStateChanged;
            _scopeView.AliensModeChanged -= OnAliensModeStateChanged;
            _scopeView.Dispose();
        }
        base.Dispose(disposing);
    }
}
