using CSxAPI.API;
using static CSxAPI.Transport.IWebSocketClient;

namespace CSxAPI;

/// <summary>
/// <para>Interface for <see cref="CSxAPIClient"/>, the top-level entry point to the <c>CSxAPI</c> library.</para>
/// <para>Use that class to connect to and send commands to a Cisco video conferencing endpoint.</para>
/// <para>Usage example:</para><para>
/// <c>await using XAPI xapi = new CSxAPIClient("192.168.1.100", "admin", "myPassword1");
/// await xapi.Connect();
///
/// await xapi.Command.Dial(number: "10990@bjn.vc");
/// </c></para>
/// <para>Find all of the xAPI functionality in <see cref="Command"/>, <see cref="Configuration"/>, <see cref="Status"/>, and <see cref="Event"/>.</para>
/// </summary>
// ReSharper disable once InconsistentNaming - the I in XAPI already stands for Interface, so adding an I prefix would be redundant
public interface XAPI: IDisposable, IAsyncDisposable {

    /// <summary>
    /// The IP address or FQDN of the Cisco endpoint on your network
    /// </summary>
    string Hostname { get; }

    /// <summary>
    /// The username used to log into the Cisco endpoint
    /// </summary>
    string Username { get; }

    /// <summary>
    /// <para>The current state of the WebSocket connection: <c>true</c> if it's connected, or <c>false</c> if <see cref="Connect"/> has not yet been called, the endpoint has disconnected, or client is reconnecting after a disconnection.</para>
    /// <para>For changes to this value, see <seealso cref="IsConnectedChanged"/>.</para>
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// <para>By default, this is <c>false</c>, so connections to WebSocket servers are rejected when the TLS certificate is untrusted, either because it's self-signed, expired, for a different hostname, from a root CA not in the trusted CA store, or any other certificate trust reason.</para>
    /// <para>If you want to connect to an endpoint with an untrusted TLS certificate anyway, set this property to <c>true</c>.</para>
    /// </summary>
    bool AllowSelfSignedTls { get; set; }

    /// <summary>
    /// Log verbose WebSocket protocol messages to the console for debugging. Defaults to <c>false</c>.
    /// </summary>
    bool ConsoleTracing { get; set; }

    /// <summary>
    /// <para>If the client is disconnected from the WebSocket server, it will attempt to automatically reconnect by default. You can observe this happening with <see cref="IsConnectedChanged"/>.</para>
    /// <para>If you want the client to stay disconnected after a connection error and not try to automatically reconnect, set this property to <c>false</c>.</para>
    /// </summary>
    bool AutoReconnect { get; set; }

    /// <summary>
    /// Call Cisco xAPI <c>xCommand</c> methods.
    /// </summary>
    ICommands Command { get; }

    /// <summary>
    /// Get, set, and listen for changes to Cisco xAPI <c>xConfiguration</c> values.
    /// </summary>
    IConfigurations Configuration { get; }

    /// <summary>
    /// Get and listen for changes to Cisco xAPI <c>xStatus</c> values.
    /// </summary>
    IStatuses Status { get; }

    /// <summary>
    /// Listen to Cisco xAPI <c>xEvent</c> emitters.
    /// </summary>
    IEvents Event { get; }

    /// <summary>
    /// <para>Fired when this client connects, reconnects, or disconnects from the WebSocket server, including the initial <see cref="Connect"/> call.</para>
    /// <para>Not fired when this instance is disposed.</para>
    /// <para>For the current value, see <seealso cref="IsConnected"/>.</para>
    /// </summary>
    event IsConnectedChangedHandler? IsConnectedChanged;

    /// <summary>
    /// <para>Connect to the Cisco endpoint's WebSocket server.</para>
    /// <para>You should call this once after constructing the <see cref="CSxAPIClient"/> instance and before sending and requests.</para>
    /// </summary>
    /// <param name="cancellationToken">if you want to stop the connection attempt while it's in progress</param>
    /// <returns>This same instance, for chaining calls.</returns>
    Task<XAPI> Connect(CancellationToken cancellationToken = default);

}