using System.Runtime.CompilerServices;
using Opcilloscope.OpcUa;

namespace Opcilloscope.Tests.Infrastructure;

/// <summary>
/// Test-assembly initialization. The in-process test server presents a self-signed
/// certificate, and the client is now secure-by-default (untrusted certs are rejected
/// unless <c>--insecure</c> is set). Integration tests connect to the local test server,
/// so they opt into accepting untrusted certs here — the equivalent of <c>--insecure</c>.
/// Runs once, before any test, when the test assembly is loaded.
/// </summary>
internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        OpcUaClientWrapper.AllowInsecureByDefault = true;
    }
}
