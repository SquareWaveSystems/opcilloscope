using Opcilloscope.App.Dialogs;
using Opcilloscope.OpcUa;

namespace Opcilloscope.Tests.Tui;

/// <summary>
/// In-process component tests for the real <see cref="ConnectDialog"/>. Exercises the
/// authentication selector (migrated from Terminal.Gui RadioGroup to OptionSelector in 2.4)
/// and the endpoint / publishing-interval fields through the dialog's public API.
/// </summary>
[Collection("Tui")]
public class ConnectDialogTests
{
    [Fact]
    public void EndpointUrl_AlwaysCarriesProtocolPrefix()
    {
        using var dialog = new ConnectDialog(initialEndpoint: "localhost:4840");

        Assert.Equal("opc.tcp://localhost:4840", dialog.EndpointUrl);
    }

    [Fact]
    public void EndpointUrl_StripsAPastedProtocolPrefix()
    {
        // A pasted endpoint that already contains the scheme must not be double-prefixed.
        using var dialog = new ConnectDialog(initialEndpoint: "opc.tcp://server:4840");

        Assert.Equal("opc.tcp://server:4840", dialog.EndpointUrl);
    }

    [Fact]
    public void PublishingInterval_IsTakenFromConstructor()
    {
        using var dialog = new ConnectDialog(initialEndpoint: "x:1", publishingInterval: 750);

        Assert.Equal(750, dialog.PublishingInterval);
    }

    [Fact]
    public void Anonymous_AuthExposesNoCredentials()
    {
        using var dialog = new ConnectDialog(initialEndpoint: "x:1", authType: AuthenticationType.Anonymous);

        Assert.Equal(AuthenticationType.Anonymous, dialog.SelectedAuthType);
        Assert.Null(dialog.Username);
        Assert.Null(dialog.Password);
    }

    [Fact]
    public void Username_AuthExposesUsername()
    {
        using var dialog = new ConnectDialog(
            initialEndpoint: "x:1",
            authType: AuthenticationType.UserName,
            username: "operator");

        Assert.Equal(AuthenticationType.UserName, dialog.SelectedAuthType);
        Assert.Equal("operator", dialog.Username);
        // The credential-hiding logic depends on an absent password staying null.
        Assert.Null(dialog.Password);
    }

    [Fact]
    public void Username_AuthExposesTypedPassword()
    {
        using var dialog = new ConnectDialog(
            initialEndpoint: "x:1",
            authType: AuthenticationType.UserName,
            username: "operator");

        // The password box is the dialog's only Secret TextField; type into it.
        var passwordField = FindSecretTextField(dialog);
        Assert.NotNull(passwordField);
        passwordField!.Text = "hunter2";

        Assert.Equal("hunter2", dialog.Password);
    }

    private static TextField? FindSecretTextField(View root) =>
        root.SubViews.OfType<TextField>().FirstOrDefault(f => f.Secret)
        ?? root.SubViews.Select(FindSecretTextField).FirstOrDefault(f => f is not null);
}
