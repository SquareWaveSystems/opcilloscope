using Terminal.Gui;

namespace Opcilloscope.App;

/// <summary>
/// Manages focus tracking across panes using polling to work around
/// Terminal.Gui v2 Enter event instability.
/// </summary>
public class FocusManager
{
    private readonly View[] _panes;
    private View? _currentPane;
    private object? _pollTimer;

    /// <summary>
    /// Fired when the focused pane changes.
    /// </summary>
    public event Action<View?>? FocusChanged;

    /// <summary>
    /// Gets the currently focused pane, or null if none.
    /// </summary>
    public View? CurrentPane => _currentPane;

    /// <summary>
    /// Creates a new FocusManager tracking the specified panes.
    /// </summary>
    /// <param name="panes">The panes to track, in tab order.</param>
    public FocusManager(params View[] panes)
    {
        _panes = panes;
    }

    /// <summary>
    /// Starts polling for focus changes (100ms interval).
    /// </summary>
    public void StartTracking()
    {
        _pollTimer = Application.AddTimeout(TimeSpan.FromMilliseconds(100), PollFocus);
    }

    /// <summary>
    /// Stops polling for focus changes.
    /// </summary>
    public void StopTracking()
    {
        if (_pollTimer != null)
        {
            Application.RemoveTimeout(_pollTimer);
            _pollTimer = null;
        }
    }

    private bool PollFocus()
    {
        var focused = Application.Top?.MostFocused;
        var newPane = FindContainingPane(focused);

        if (newPane != _currentPane)
        {
            _currentPane = newPane;
            FocusChanged?.Invoke(newPane);
        }

        return true; // Keep polling
    }

    private View? FindContainingPane(View? view)
    {
        while (view != null)
        {
            if (_panes.Contains(view))
                return view;
            view = view.SuperView;
        }
        return null;
    }

    /// <summary>
    /// Sets focus to the pane at the specified index.
    /// </summary>
    private void FocusPane(int index)
    {
        if (index >= 0 && index < _panes.Length)
        {
            _panes[index].SetFocus();
        }
    }

    /// <summary>
    /// Cycles focus to the next pane.
    /// </summary>
    public void FocusNext()
    {
        var currentIndex = _currentPane != null ? Array.IndexOf(_panes, _currentPane) : -1;
        FocusPane(GetNextIndex(currentIndex, _panes.Length));
    }

    /// <summary>
    /// Computes the index of the next pane in tab order, wrapping around to the
    /// first pane after the last. Pure helper extracted for testability.
    /// </summary>
    internal static int GetNextIndex(int currentIndex, int paneCount)
    {
        return (currentIndex + 1) % paneCount;
    }
}
