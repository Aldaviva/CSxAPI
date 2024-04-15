namespace CSxAPI.API.Exceptions;

// Super exceptions
//
public abstract class XapiException(string message, string hostname, Exception cause): ApplicationException(message, cause) {

    public string Hostname { get; init; } = hostname;

}

public class NetworkException(string         message, string hostname, Exception cause): XapiException(message, hostname, cause);
public abstract class ClientException(string message, string hostname, Exception cause): XapiException(message, hostname, cause);

// Network exceptions
//
public class ConnectionRefusedException(string  hostname, Exception cause): NetworkException($"Connection refused to WebSocket server {hostname}", hostname, cause);
public class InvalidCertificateException(string hostname, Exception cause): NetworkException($"Invalid TLS certificate on WebSocket server {hostname}", hostname, cause);
public class TimeOutException(string            hostname, Exception cause): NetworkException($"Timed out while connecting to WebSocket server {hostname}", hostname, cause);
public class UnknownHostException(string        hostname, Exception cause): NetworkException($"Unknown host for WebSocket server {hostname}", hostname, cause);
public class NoRouteToHostException(string      hostname, Exception cause): NetworkException($"No route to host for WebSocket server {hostname}", hostname, cause);

public class DisconnectedException: NetworkException {

    public string CommandName { get; }

    public DisconnectedException(string hostname, object[] path, Exception cause): this(hostname, path.JoinPath(), cause) { }

    private DisconnectedException(string hostname, string path, Exception cause): base(
        $"Connection to endpoint {hostname} disconnected while running \"{path}\" command, and the {nameof(CSxAPIClient)} instance had {nameof(CSxAPIClient.AutoReconnect)} disabled", hostname,
        cause) {
        CommandName = path;
    }

}

// Client exceptions
//
public class CommandNotFoundException: ClientException {

    public string CommandName { get; }

    public CommandNotFoundException(string hostname, object[] path, Exception cause): this(hostname, path.JoinPath(), cause) { }

    private CommandNotFoundException(string hostname, string name, Exception cause): base(
        $"xAPI command \"{name}\" was not found on the endpoint {hostname}, it may not be available on the software version or hardware of the endpoint", hostname, cause) {
        CommandName = name;
    }

}

public class IllegalArgumentException: ClientException {

    public string CommandName { get; }

    public IllegalArgumentException(string hostname, object[] path, IDictionary<string, object?>? arguments, Exception cause): this(hostname, path.JoinPath(), arguments, cause) { }

    public IllegalArgumentException(string hostname, object[] path, object argument, Exception cause):
        this(hostname, path.JoinPath(), new Dictionary<string, object?> { { "value", argument } }, cause) { }

    private IllegalArgumentException(string hostname, string path, IDictionary<string, object?>? arguments, Exception cause): base(
        $"Illegal argument passed to \"{path}\" on endpoint {hostname} {{ {(arguments == null ? "null" : string.Join(", ", arguments.Select(pair => $"{pair.Key}={pair.Value}")))} }}", hostname,
        cause) {
        CommandName = path;
    }

}

public class AuthenticationException(string hostname, string username, Exception cause)
    : ClientException($"Not authenticated with username {username}, or incorrect hostname, for WebSocket server {hostname}", hostname, cause) {

    public string Username { get; } = username;

}