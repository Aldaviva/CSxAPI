using CSxAPI.API;
using CSxAPI.Transport;
using System.Net;

// ReSharper disable MemberCanBePrivate.Global - this is a public API

namespace CSxAPI;

/// <summary>
/// <para>Top-level entry point for the <c>CSxAPI</c> library. Use this class to connect to and send commands to a Cisco video conferencing endpoint.</para>
/// <para>Usage example:</para><para>
/// <c>await using XAPI xapi = new CSxAPIClient("192.168.1.100", "admin", "myPassword1");
/// await xapi.Connect();
///
/// await xapi.Command.Dial(number: "10990@bjn.vc");
/// </c></para>
/// <para>Find all of the xAPI functionality in <see cref="Command"/>, <see cref="Configuration"/>, <see cref="Status"/>, and <see cref="Event"/>.</para>
/// </summary>
public class CSxAPIClient: XAPI {

    /// <inheritdoc />
    public string Hostname { get; }

    /// <inheritdoc />
    public string Username => _credentials.UserName;

    /// <inheritdoc />
    public bool IsConnected => Transport.IsConnected;

    /// <inheritdoc />
    public bool AllowSelfSignedTls {
        get => Transport.AllowSelfSignedTls;
        set => Transport.AllowSelfSignedTls = value;
    }

    /// <inheritdoc />
    public bool ConsoleTracing {
        get => Transport.ConsoleTracing;
        set => Transport.ConsoleTracing = value;
    }

    /// <inheritdoc />
    public bool AutoReconnect {
        get => Transport.AutoReconnect;
        set => Transport.AutoReconnect = value;
    }

    /// <inheritdoc />
    public ICommands Command { get; }

    /// <inheritdoc />
    public IConfigurations Configuration { get; }

    /// <inheritdoc />
    public IStatuses Status { get; }

    /// <inheritdoc />
    public IEvents Event { get; }

    /// <inheritdoc />
    public event IWebSocketClient.IsConnectedChangedHandler? IsConnectedChanged {
        add => Transport.IsConnectedChanged += value;
        remove => Transport.IsConnectedChanged -= value;
    }

    private readonly NetworkCredential _credentials;

    /// <summary>
    /// Underlying WebSocket connection
    /// </summary>
    protected readonly IWebSocketXapi Transport;

    /// <summary>
    /// Underlying feedback event handler
    /// </summary>
    protected readonly IFeedbackSubscriber FeedbackSubscriber;

    /// <summary>
    /// Create a new xAPI client to connect to a Cisco video conferencing endpoint
    /// </summary>
    /// <param name="hostname">IP address or FQDN of the Cisco endpoint</param>
    /// <param name="username">Username used to log in to the Cisco endpoint, such as <c>admin</c>. If your endpoint is registered to Webex, you must use Local Device Controls to create a new local user.</param>
    /// <param name="password">User's password</param>
    public CSxAPIClient(string hostname, string username, string password): this(hostname, new NetworkCredential(username, password)) { }

    /// <summary>
    /// Create a new xAPI client to connect to a Cisco video conferencing endpoint
    /// </summary>
    /// <param name="hostname">IP address or FQDN of the Cisco endpoint</param>
    /// <param name="credentials">Username and password of a user account on the Cisco endpoint. If your endpoint is registered to Webex, you must use Local Device Controls to create a new local user.</param>
    public CSxAPIClient(string hostname, NetworkCredential credentials) {
        Hostname           = hostname;
        _credentials       = credentials;
        Transport          = new WebSocketXapi(Hostname, _credentials);
        FeedbackSubscriber = new FeedbackSubscriber(Transport);
        Command            = new Commands(Transport);
        Configuration      = new Configurations(Transport, FeedbackSubscriber);
        Status             = new Statuses(Transport, FeedbackSubscriber);
        Event              = new Events(FeedbackSubscriber);
    }

    /// <inheritdoc />
    public async Task<XAPI> Connect(CancellationToken cancellationToken = default) {
        await Transport.Connect(cancellationToken).ConfigureAwait(false);
        return this;
    }

    /// <inheritdoc cref="Dispose()" />
    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            FeedbackSubscriber.Dispose();
            Transport.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() {
        GC.SuppressFinalize(this);
        FeedbackSubscriber.Dispose();
        return Transport.DisposeAsync();
    }

}