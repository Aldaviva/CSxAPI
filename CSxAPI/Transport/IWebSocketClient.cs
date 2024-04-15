using StreamJsonRpc;

namespace CSxAPI.Transport;

public interface IWebSocketClient {

    bool ConsoleTracing { get; set; }

    bool AllowSelfSignedTls { get; set; }

    Task Connect(CancellationToken? cancellationToken = null);

    bool IsConnected { get; }
    bool AutoReconnect { get; set; }

    event IsConnectedChangedHandler? IsConnectedChanged;

    delegate void IsConnectedChangedHandler(bool isConnected, JsonRpcDisconnectedEventArgs? disconnectionDetails);

}