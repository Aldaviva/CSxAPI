using CSxAPI.API;
using StreamJsonRpc;

namespace CSxAPI;

// ReSharper disable once InconsistentNaming - the I in XAPI already stands for Interface, so IXAPI would be redundant
public interface XAPI: IDisposable, IAsyncDisposable {

    string Hostname { get; }
    string Username { get; }
    bool IsConnected { get; }
    bool AllowSelfSignedTls { get; set; }
    bool ConsoleTracing { get; set; }

    ICommands Command { get; }
    IConfigurations Configuration { get; }
    IStatuses Status { get; }
    IEvents Event { get; }

    event EventHandler<JsonRpcDisconnectedEventArgs>? Disconnected;

    Task<XAPI> Connect(CancellationToken cancellationToken = default);

}