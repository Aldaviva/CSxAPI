using CSxAPI.API;
using static CSxAPI.Transport.IWebSocketClient;

namespace CSxAPI;

// ReSharper disable once InconsistentNaming - the I in XAPI already stands for Interface, so IXAPI would be redundant
public interface XAPI: IDisposable, IAsyncDisposable {

    string Hostname { get; }
    string Username { get; }
    bool IsConnected { get; }
    bool AllowSelfSignedTls { get; set; }
    bool ConsoleTracing { get; set; }
    bool AutoReconnect { get; set; }

    ICommands Command { get; }
    IConfigurations Configuration { get; }
    IStatuses Status { get; }
    IEvents Event { get; }

    event IsConnectedChangedHandler? IsConnectedChanged;

    Task<XAPI> Connect(CancellationToken cancellationToken = default);

}