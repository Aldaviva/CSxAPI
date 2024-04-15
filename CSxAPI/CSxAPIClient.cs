using CSxAPI.API;
using CSxAPI.Transport;
using System.Net;

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
    public bool AutoReconnect {
        get => _transport.AutoReconnect;
        set => _transport.AutoReconnect = value;
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
        add => _transport.IsConnectedChanged += value;
        remove => _transport.IsConnectedChanged -= value;
    }

    private readonly NetworkCredential  _credentials;
    private readonly IWebSocketXapi     _transport;
    private readonly FeedbackSubscriber _feedbackSubscriber;

    public CSxAPIClient(string hostname, string username, string password): this(hostname, new NetworkCredential(username, password)) { }

    public CSxAPIClient(string hostname, NetworkCredential credentials) {
        Hostname     = hostname;
        _credentials = credentials;
        _transport   = new WebSocketXapi(Hostname, _credentials);

        _feedbackSubscriber = new FeedbackSubscriber(_transport);

        Command       = new Commands(_transport);
        Configuration = new Configurations(_transport, _feedbackSubscriber);
        Status        = new Statuses(_transport, _feedbackSubscriber);
        Event         = new Events(_feedbackSubscriber);
    }

    /// <inheritdoc />
    public async Task<XAPI> Connect(CancellationToken cancellationToken = default) {
        await _transport.Connect(cancellationToken).ConfigureAwait(false);
        return this;
    }

    /// <inheritdoc cref="Dispose()" />
    protected virtual void Dispose(bool disposing) {
        if (disposing) {
            _feedbackSubscriber.Dispose();
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
        _feedbackSubscriber.Dispose();
        return _transport.DisposeAsync();
    }

}