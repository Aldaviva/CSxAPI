using StreamJsonRpc;

namespace CSxAPI.Transport;

/// <summary>
/// A generic client interface for any WebSocket connection.
/// </summary>
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

    /// <summary>
    /// Method signature of the event subscriber for changes to the connection state. Fired when the WebSocket client connects to or disconnects from the server.
    /// </summary>
    /// <param name="isConnected"><c>true</c> if the client just connected or reconnected to the WebSocket server, or <c>false</c> if it just disconnected.</param>
    /// <param name="disconnectionDetails">if <paramref name="isConnected"/> is <c>false</c>, this will contain details about why the client was disconnected, otherwise <c>null</c>.</param>
    delegate void IsConnectedChangedHandler(bool isConnected, JsonRpcDisconnectedEventArgs? disconnectionDetails);

}