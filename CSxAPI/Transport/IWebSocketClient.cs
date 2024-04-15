using StreamJsonRpc;

namespace CSxAPI.Transport;

public interface IWebSocketClient {

    /// <inheritdoc cref="XAPI.ConsoleTracing"/>
    bool ConsoleTracing { get; set; }

    /// <inheritdoc cref="XAPI.AllowSelfSignedTls"/>
    bool AllowSelfSignedTls { get; set; }

    /// <inheritdoc cref="XAPI.Connect"/>
    Task Connect(CancellationToken? cancellationToken = null);

    /// <inheritdoc cref="XAPI.IsConnected"/>
    bool IsConnected { get; }

    /// <inheritdoc cref="XAPI.AutoReconnect"/>
    bool AutoReconnect { get; set; }

    /// <inheritdoc cref="XAPI.IsConnectedChanged"/>
    event IsConnectedChangedHandler? IsConnectedChanged;

    delegate void IsConnectedChangedHandler(bool isConnected, JsonRpcDisconnectedEventArgs? disconnectionDetails);

}