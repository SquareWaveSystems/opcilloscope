using Opcilloscope.App.Views;
using Opcilloscope.OpcUa.Models;

namespace Opcilloscope.Tests.Tui;

/// <summary>
/// In-process component tests that construct the real <see cref="MonitoredVariablesView"/>
/// (a Terminal.Gui v2 TableView-backed view) and exercise its public data API.
///
/// NOTE on rendered-cell assertions: Terminal.Gui 2.4.5 (stable) does not expose a public
/// headless driver — <c>Application.Create()</c> leaves <c>Driver</c> null until the real
/// console event loop runs, and the develop-branch <c>DriverAssert</c> helper is not shipped.
/// So these tests assert on observable component behaviour/state; assertions on the *rendered*
/// screen are covered by the black-box PTY end-to-end suite (Opcilloscope.E2ETests).
///
/// Terminal.Gui's Application is global mutable state, so all TUI tests share a
/// non-parallel collection (see <see cref="TuiCollection"/>).
/// </summary>
[Collection("Tui")]
public class MonitoredVariablesViewTests
{
    private static MonitoredNode Node(
        uint handle,
        string name,
        bool scope = false,
        long connectionGeneration = 0) => new()
        {
            ClientHandle = handle,
            ConnectionGeneration = connectionGeneration,
            NodeId = $"ns=2;s={name}",
            DisplayName = name,
            Value = name,
            IsSelectedForScope = scope,
        };

    [Fact]
    public void NewView_StartsEmpty()
    {
        using var view = new MonitoredVariablesView();

        Assert.Null(view.SelectedVariable);
        Assert.Empty(view.ScopeSelectedNodes);
        Assert.Equal(0, view.ScopeSelectionCount);
    }

    [Fact]
    public void AddVariable_AddsScopeSelectedNodeToScopeCollection()
    {
        using var view = new MonitoredVariablesView();

        view.AddVariable(Node(1, "Counter", scope: true));
        view.AddVariable(Node(2, "SineWave", scope: false));

        Assert.Equal(1, view.ScopeSelectionCount);
        Assert.Single(view.ScopeSelectedNodes);
        Assert.Equal("Counter", view.ScopeSelectedNodes[0].DisplayName);
    }

    [Fact]
    public void AddVariable_IsIdempotentPerClientHandle()
    {
        using var view = new MonitoredVariablesView();

        view.AddVariable(Node(1, "Counter", scope: true));
        view.AddVariable(Node(1, "Counter", scope: true)); // same handle - ignored

        Assert.Equal(1, view.ScopeSelectionCount);
    }

    [Fact]
    public void RemoveVariable_RemovesFromScopeSelection()
    {
        using var view = new MonitoredVariablesView();
        view.AddVariable(Node(1, "Counter", scope: true));
        view.AddVariable(Node(2, "SineWave", scope: true));

        view.RemoveVariable(1);

        Assert.Single(view.ScopeSelectedNodes);
        Assert.Equal("SineWave", view.ScopeSelectedNodes[0].DisplayName);
    }

    [Fact]
    public void ProcessPendingUpdates_UpdateArrivesDuringIdleTransition_KeepsProcessingAlive()
    {
        using var view = new MonitoredVariablesView();
        var variable = Node(1, "initial");
        view.AddVariable(variable);
        variable.Value = "first";
        view.UpdateVariable(variable);

        var keepRunning = view.ProcessPendingUpdatesForTest(
            () =>
            {
                variable.Value = "second";
                view.UpdateVariable(variable);
            });

        Assert.True(keepRunning);
        Assert.Equal(1, view.PendingUpdateCountForTest);

        Assert.False(view.ProcessPendingUpdatesForTest());
        Assert.Equal("second", view.GetDisplayedValueForTest(1));
    }

    [Theory]
    [InlineData(1L)] // Fresh manager created by a reconnect fallback in the same generation.
    [InlineData(2L)] // Fresh manager created by a later connection generation.
    public void Clear_InvalidatesQueuedAndLateOldSessionUpdatesBeforeClientHandleReuse(
        long newConnectionGeneration)
    {
        var timerToken = new object();
        Func<bool>? timerCallback = null;
        object? removedTimer = null;
        using var view = new MonitoredVariablesView(
            (_, callback) =>
            {
                timerCallback = callback;
                return timerToken;
            },
            token => removedTimer = token);

        var oldVariable = Node(1, "old-live", connectionGeneration: 1);
        view.AddVariable(oldVariable);
        oldVariable.Value = "old-pending";
        view.UpdateVariable(oldVariable);
        Assert.NotNull(timerCallback);

        view.Clear();
        view.AddVariable(Node(
            1,
            "new-session",
            connectionGeneration: newConnectionGeneration));

        // A notification already in flight from the old SubscriptionManager can
        // reach the view after Clear. Its reused handle must not target the new row.
        oldVariable.Value = "late-old-session-update";
        view.UpdateVariable(oldVariable);

        Assert.Same(timerToken, removedTimer);
        Assert.False(timerCallback!());
        Assert.False(view.ProcessPendingUpdatesForTest());
        Assert.Equal(0, view.PendingUpdateCountForTest);
        Assert.Equal("new-session", view.GetDisplayedValueForTest(1));
    }

    [Fact]
    public void ReconcileVariables_RebuildsMembershipAndPreservesScopeSelection()
    {
        using var view = new MonitoredVariablesView();
        var retained = Node(2, "retained", connectionGeneration: 2, scope: true);
        view.AddVariable(Node(1, "removed", connectionGeneration: 1));

        view.ReconcileVariables([retained]);

        Assert.Null(view.GetDisplayedValueForTest(1));
        Assert.Equal("retained", view.GetDisplayedValueForTest(2));
        Assert.True(retained.IsSelectedForScope);
        Assert.Equal(1, view.ScopeSelectionCount);
    }
}
