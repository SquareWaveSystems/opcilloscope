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
    private static MonitoredNode Node(uint handle, string name, bool scope = false) => new()
    {
        ClientHandle = handle,
        NodeId = $"ns=2;s={name}",
        DisplayName = name,
        IsSelectedForScope = scope,
    };

    [Fact]
    public void NewView_StartsEmpty()
    {
        var view = new MonitoredVariablesView();

        Assert.Null(view.SelectedVariable);
        Assert.Empty(view.ScopeSelectedNodes);
        Assert.Equal(0, view.ScopeSelectionCount);
    }

    [Fact]
    public void AddVariable_AddsScopeSelectedNodeToScopeCollection()
    {
        var view = new MonitoredVariablesView();

        view.AddVariable(Node(1, "Counter", scope: true));
        view.AddVariable(Node(2, "SineWave", scope: false));

        Assert.Equal(1, view.ScopeSelectionCount);
        Assert.Single(view.ScopeSelectedNodes);
        Assert.Equal("Counter", view.ScopeSelectedNodes[0].DisplayName);
    }

    [Fact]
    public void AddVariable_IsIdempotentPerClientHandle()
    {
        var view = new MonitoredVariablesView();

        view.AddVariable(Node(1, "Counter", scope: true));
        view.AddVariable(Node(1, "Counter", scope: true)); // same handle - ignored

        Assert.Equal(1, view.ScopeSelectionCount);
    }

    [Fact]
    public void RemoveVariable_RemovesFromScopeSelection()
    {
        var view = new MonitoredVariablesView();
        view.AddVariable(Node(1, "Counter", scope: true));
        view.AddVariable(Node(2, "SineWave", scope: true));

        view.RemoveVariable(1);

        Assert.Single(view.ScopeSelectedNodes);
        Assert.Equal("SineWave", view.ScopeSelectedNodes[0].DisplayName);
    }
}
