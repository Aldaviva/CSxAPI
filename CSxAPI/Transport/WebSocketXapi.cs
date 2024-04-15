using CSxAPI.API.Exceptions;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Text;
using NetworkException = CSxAPI.API.Exceptions.NetworkException;

namespace CSxAPI.Transport;

public class WebSocketXapi(string hostname, NetworkCredential credentials): IWebSocketXapi {

    /// <inheritdoc />
    public string Hostname { get; } = hostname;

    /// <inheritdoc />
    public string Username => credentials.UserName;

    /// <inheritdoc />
    public bool AllowSelfSignedTls { get; set; } = false;

    /// <inheritdoc />
    public bool AutoReconnect { get; set; } = true;

    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <inheritdoc />
    public bool ConsoleTracing {
        get => _consoleTracing;
        set {
            _consoleTracing = value;
            applyConsoleTracing();
        }
    }

    /// <inheritdoc />
    public event IWebSocketClient.IsConnectedChangedHandler? IsConnectedChanged;

    /// <inheritdoc />
    public event IWebSocketXapi.SubscriptionPublishedArgs? SubscriptionPublished;

    private readonly TraceListener _consoleTraceListener = new ConsoleTraceListener();

    private bool                  _consoleTracing;
    private JsonRpc?              _jsonRpc;
    private ClientWebSocket       _webSocket = null!;
    private bool                  _disposed;
    private TaskCompletionSource? _reconnected;

    /// <inheritdoc />
    public async Task Connect(CancellationToken? cancellationToken = null) {
        _webSocket = new ClientWebSocket();

        _webSocket.Options.SetRequestHeader("Authorization",
            "Basic " + Convert.ToBase64String(new UTF8Encoding(false, true).GetBytes(credentials.UserName + ":" + credentials.Password), Base64FormattingOptions.None));
        _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(5);
        if (AllowSelfSignedTls) {
            _webSocket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        }

        _jsonRpc = new JsonRpc(new WebSocketMessageHandler(_webSocket));
        applyConsoleTracing();
        _jsonRpc.AddLocalRpcMethod("xFeedback/Event", OnFeedbackEvent);
        _jsonRpc.Disconnected += OnDisconnection;

        // TODO catch and rethrow exceptions for wrong hostname and password
        try {
            await _webSocket.ConnectAsync(new UriBuilder("wss", Hostname, -1, "ws").Uri, cancellationToken ?? CancellationToken.None).ConfigureAwait(false);
        } catch (WebSocketException e) {
            SocketError? socketError = (e.InnerException?.InnerException as SocketException)?.SocketErrorCode;
            throw e.WebSocketErrorCode switch {
                WebSocketError.NotAWebSocket                                                                => new NotAuthorizedException(Hostname, Username, e),
                WebSocketError.Faulted when socketError is SocketError.TimedOut                             => new TimeOutException(Hostname, e),
                WebSocketError.Faulted when socketError is SocketError.HostNotFound                         => new UnknownHostException(Hostname, e),
                WebSocketError.Faulted when socketError is SocketError.HostUnreachable                      => new NoRouteToHostException(Hostname, e),
                WebSocketError.Faulted when socketError is SocketError.ConnectionRefused                    => new ConnectionRefusedException(Hostname, e),
                WebSocketError.Faulted when e is { InnerException.InnerException: AuthenticationException } => new InvalidCertificateException(Hostname, e),
                /*{
                    Message: "The remote certificate is invalid according to the validation procedure: RemoteCertificateNameMismatch, RemoteCertificateChainErrors"
                    or "The remote certificate was rejected by the provided RemoteCertificateValidationCallback."
                  }*/
                _ => new NetworkException($"Unknown WebSocket error while connecting to \"{Hostname}\": {e.Message}", Hostname, e)
            };
        }

        _jsonRpc.StartListening();
        IsConnected = true;
        IsConnectedChanged?.Invoke(true, null);
        _reconnected?.SetResult();
    }

    private void OnDisconnection(object? sender, JsonRpcDisconnectedEventArgs args) => _ = Task.Run(async () => {
        if (!_disposed) {
            IsConnected = false;
            IsConnectedChanged?.Invoke(false, args);

            if (AutoReconnect) {
                _reconnected = new TaskCompletionSource();
                while (AutoReconnect && !IsConnected && !_disposed) {
                    DisposeWebSocket();
                    try {
                        await Connect().ConfigureAwait(false);
                    } catch (WebSocketException) {
                        await Task.Delay(2000).ConfigureAwait(false);
                        // try again
                    }
                }
            }
        }
    });

    /**
     * StreamJsonRpc insists on matching parameter names, so provide lots of optional parameters with all possible names, and just pick the one that isn't null
     */
    [SuppressMessage("ReSharper", "InconsistentNaming")] // parameter names must exactly match what the endpoint sends in JSON, case sensitively, or this method won't be called
    private void OnFeedbackEvent(long Id, JObject? Event = null, JObject? Status = null, JObject? Configuration = null) {
        SubscriptionPublished?.Invoke(Id, (Event ?? Status ?? Configuration)!);
    }

    private void applyConsoleTracing() {
        if (_jsonRpc != null) {
            TraceSource traceSource = _jsonRpc.TraceSource;

            traceSource.Switch.Level = _consoleTracing ? SourceLevels.All : SourceLevels.Warning | SourceLevels.ActivityTracing;
            if (!_consoleTracing) {
                traceSource.Listeners.Remove(_consoleTraceListener);
            } else if (!traceSource.Listeners.Contains(_consoleTraceListener)) {
                traceSource.Listeners.Add(_consoleTraceListener);
            }
        }
    }

    public Task<T> Get<T>(params object[] path) {
        return _jsonRpc!.InvokeWithParameterObjectAsync<T>("xGet", new { Path = NormalizePath(path) });
    }

    public Task<T> Query<T>(params object[] path) {
        return _jsonRpc!.InvokeWithParameterObjectAsync<T>("xQuery", new { Path = NormalizePath(path) });
    }

    public Task<bool> Set(IEnumerable<object> path, object value) {
        return _jsonRpc!.InvokeWithParameterObjectAsync<bool>("xSet", new { Path = NormalizePath(path), Value = value });
    }

    public Task<T> Command<T>(IEnumerable<object> path, object? parameters = null) {
        return _jsonRpc!.InvokeWithParameterObjectAsync<T>(GetCommandMethod(path), parameters);
    }

    public Task Command(IEnumerable<object> path, object? parameters = null) {
        return _jsonRpc!.InvokeWithParameterObjectAsync(GetCommandMethod(path), parameters);
    }

    public async Task<long> Subscribe(object[] path, bool notifyCurrentValue = false) {
        try {
            IDictionary<string, object> subscription = await _jsonRpc!.InvokeWithParameterObjectAsync<IDictionary<string, object>>("xFeedback/Subscribe", new {
                Query              = NormalizePath(path),
                NotifyCurrentValue = notifyCurrentValue
            }).ConfigureAwait(false);
            return (long) subscription["Id"];
        } catch (ConnectionLostException e) {
            if (AutoReconnect && _reconnected != null) {
                await _reconnected.Task.ConfigureAwait(false);
                return await Subscribe(path, notifyCurrentValue).ConfigureAwait(false);
            } else {
                throw new DisconnectedException(Hostname, path, e);
            }
        }
    }

    /// <inheritdoc />
    public Task<bool> Unsubscribe(long subscriptionId) {
        return _jsonRpc!.InvokeWithParameterObjectAsync<bool>("xFeedback/Unsubscribe", new { Id = subscriptionId });
    }

    private static string GetCommandMethod(IEnumerable<object> path) {
        return string.Join("/", path
            .SkipWhile((item, i) =>
                i == 0 && item is string s && (string.Equals(s, "Command", StringComparison.InvariantCultureIgnoreCase) || string.Equals(s, "xCommand", StringComparison.InvariantCultureIgnoreCase)))
            .Prepend("xCommand"));
    }

    private static IEnumerable<object> NormalizePath(IEnumerable<object> path) {
        return path.Select((item, index) => index == 0 && item is string s ? s.TrimStart('x') : item);
    }

    /// <inheritdoc />
    public async Task<T> GetConfigurationOrStatus<T>(object[] path) {
        try {
            return await Get<T>(path).ConfigureAwait(false);
        } catch (RemoteMethodNotFoundException e) {
            throw new CommandNotFoundException(Hostname, path, e);
        } catch (ConnectionLostException e) {
            if (AutoReconnect && _reconnected != null) {
                await _reconnected.Task.ConfigureAwait(false);
                return await GetConfigurationOrStatus<T>(path).ConfigureAwait(false);
            } else {
                throw new DisconnectedException(Hostname, path, e);
            }
        }
    }

    /// <inheritdoc />
    public async Task SetConfiguration(object[] path, object newValue) {
        try {
            await Set(path, newValue).ConfigureAwait(false);
        } catch (RemoteMethodNotFoundException e) {
            throw new CommandNotFoundException(Hostname, path, e);
        } catch (RemoteInvocationException e) {
            if ((e.ErrorData as JObject)?.GetValue("Cause")?.Value<int>() == 27) {
                throw new IllegalArgumentException(Hostname, path, newValue, e);
            }

            throw;
        } catch (ConnectionLostException e) {
            if (AutoReconnect && _reconnected != null) {
                await _reconnected.Task.ConfigureAwait(false);
                await SetConfiguration(path, newValue).ConfigureAwait(false);
            } else {
                throw new DisconnectedException(Hostname, path, e);
            }
        }
    }

    /// <inheritdoc />
    public async Task<IDictionary<string, object>> CallMethod(object[] path, IDictionary<string, object?>? parameters) {
        try {
            return await Command<IDictionary<string, object>>(path, parameters?.Compact()).ConfigureAwait(false);
        } catch (RemoteMethodNotFoundException e) {
            throw new CommandNotFoundException(Hostname, path, e);
        } catch (RemoteInvocationException e) {
            if ((e.ErrorData as JObject)?.GetValue("Cause")?.Value<int>() == 27) {
                throw new IllegalArgumentException(Hostname, path, parameters, e);
            }

            throw;
        } catch (ConnectionLostException e) {
            if (AutoReconnect && _reconnected != null) {
                await _reconnected.Task.ConfigureAwait(false);
                return await CallMethod(path, parameters).ConfigureAwait(false);
            } else {
                throw new DisconnectedException(Hostname, path, e);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        _disposed = true;
        DisposeWebSocket();
        _consoleTraceListener.Dispose();
        GC.SuppressFinalize(this);
    }

    private void DisposeWebSocket() {
        _webSocket.Dispose();
        if (_jsonRpc != null) {
            _jsonRpc.Disconnected -= OnDisconnection;
            _jsonRpc.Dispose();
            _jsonRpc = null;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() {
        Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

}