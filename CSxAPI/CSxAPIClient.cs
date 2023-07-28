using System.Net;
using CSxAPI.API;
using CSxAPI.Transport;
using StreamJsonRpc;

namespace CSxAPI;

public class CSxAPIClient: XAPI {

    /// <inheritdoc />
    public string Hostname { get; }

    /// <inheritdoc />
    public string Username => _credentials.UserName;

    /// <inheritdoc />
    public bool IsConnected => _transport.IsConnected;

    /// <inheritdoc />
    public bool AllowSelfSignedTls {
        get => _transport.AllowSelfSignedTls;
        set => _transport.AllowSelfSignedTls = value;
    }

    /// <inheritdoc />
    public bool ConsoleTracing {
        get => _transport.ConsoleTracing;
        set => _transport.ConsoleTracing = value;
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
    public event EventHandler<JsonRpcDisconnectedEventArgs>? Disconnected {
        add => _transport.Disconnected += value;
        remove => _transport.Disconnected -= value;
    }

    private readonly NetworkCredential _credentials;
    private readonly IWebSocketXapi    _transport;

    public CSxAPIClient(string hostname, string username, string password): this(hostname, new NetworkCredential(username, password)) { }

    public CSxAPIClient(string hostname, NetworkCredential credentials) {
        Hostname     = hostname;
        _credentials = credentials;
        _transport   = new WebSocketXapi(Hostname, _credentials);

        FeedbackSubscriber feedbackSubscriber = new(_transport);

        Command       = new Commands(_transport);
        Configuration = new Configurations(_transport, feedbackSubscriber);
        Status        = new Statuses(_transport, feedbackSubscriber);
        Event         = new Events(feedbackSubscriber);
    }

    /// <inheritdoc />
    public async Task<XAPI> Connect(CancellationToken cancellationToken = default) {
        await _transport.Connect(cancellationToken).ConfigureAwait(false);
        return this;
    }

    /// <inheritdoc cref="Dispose()" />
    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            _transport.Dispose();
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
        return _transport.DisposeAsync();
    }

}