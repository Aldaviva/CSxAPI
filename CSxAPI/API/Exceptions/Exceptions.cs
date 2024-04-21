namespace CSxAPI.API.Exceptions;

/*
 * Super exceptions
 */

/// <summary>
/// An exception occurred in the <c>CSxAPI</c> library while communicating with a Cisco endpoint. This is the superclass of all exceptions thrown by this library. Subclasses contain more details.
/// </summary>
/// <param name="message">Description of the error</param>
/// <param name="hostname">The FQDN or IP address of the Cisco endpoint</param>
/// <param name="cause">The underlying exception</param>
public abstract class XapiException(string message, string hostname, Exception cause): ApplicationException(message, cause) {

    /// <summary>
    /// The FQDN or IP address of the Cisco endpoint
    /// </summary>
    public string Hostname { get; init; } = hostname;

}

/// <summary>
/// Superclass of exceptions thrown by the <c>CSxAPI</c> library when there is an issue with the connection to the Cisco endpoint, such as socket or TLS exceptions.
/// </summary>
/// <inheritdoc cref="XapiException"/>
public class NetworkException(string message, string hostname, Exception cause): XapiException(message, hostname, cause);

/// <summary>
/// Superclass of exceptions thrown by the <c>CSxAPI</c> library when there is an issue with the usage of xAPI by the client, such as passing invalid arguments to a command.
/// </summary>
/// <inheritdoc cref="XapiException"/>
public abstract class ClientException(string message, string hostname, Exception cause): XapiException(message, hostname, cause);

/*
 * Network exceptions
 */

/// <summary>
/// Failed to connect to the endpoint due to a Connection Refused socket error.
/// </summary>
/// <inheritdoc cref="NetworkException"/>
public class ConnectionRefusedException(string hostname, Exception cause): NetworkException($"Connection refused to WebSocket server {hostname}", hostname, cause);

/// <summary>
/// <para>Failed to connect to the endpoint due to an expired, self-signed, or otherwise untrusted X.509 certificate on the TLS server.</para>
/// <para>To connect to WebSocket servers that use self-signed TLS certificates, set <see cref="CSxAPIClient.AllowSelfSignedTls"/> to <c>true</c>.</para>
/// </summary>
/// <inheritdoc cref="NetworkException"/>
public class InvalidCertificateException(string hostname, Exception cause): NetworkException($"Invalid TLS certificate on WebSocket server {hostname}", hostname, cause);

/// <summary>
/// The endpoint's WebSocket server did not respond in the given time period.
/// </summary>
/// <inheritdoc cref="NetworkException"/>
public class TimeOutException(string hostname, Exception cause): NetworkException($"Timed out while connecting to WebSocket server {hostname}", hostname, cause);

/// <summary>
/// The endpoint's hostname could not be found in DNS.
/// </summary>
/// <inheritdoc cref="NetworkException"/>
public class UnknownHostException(string hostname, Exception cause): NetworkException($"Unknown host for WebSocket server {hostname}", hostname, cause);

/// <summary>
/// Failed to connect to the endpoint because there was no network route to the host
/// </summary>
/// <inheritdoc cref="NetworkException"/>
public class NoRouteToHostException(string hostname, Exception cause): NetworkException($"No route to host for WebSocket server {hostname}", hostname, cause);

/// <summary>
/// <para>The WebSocket connection to the endpoint was dropped.</para>
/// <para>To automatically reconnect instead of throwing this exception, set <see cref="CSxAPIClient.AutoReconnect"/> to <c>true</c>.</para>
/// </summary>
public class DisconnectedException: NetworkException {

    /// <summary>
    /// The space-separated name of the xAPI command that failed to execute when the disconnection occurred, such as <c>xCommand Dial</c>.
    /// </summary>
    public string CommandName { get; }

    /// <inheritdoc cref="DisconnectedException"/>
    /// <param name="hostname">The FQDN or IP address of the Cisco endpoint</param>
    /// <param name="path">list of elements in the command's path, such as <c>["xCommand", "Dial"]</c></param>
    /// <param name="cause">The underlying exception</param>
    public DisconnectedException(string hostname, object[] path, Exception cause): this(hostname, path.JoinPath(), cause) { }

    private DisconnectedException(string hostname, string path, Exception cause): base(
        $"Connection to endpoint {hostname} disconnected while running \"{path}\" command, and the {nameof(CSxAPIClient)} instance had {nameof(CSxAPIClient.AutoReconnect)} disabled", hostname,
        cause) {
        CommandName = path;
    }

}

/*
 * Client exceptions
 */

/// <summary>
/// <para>The client sent a command that the server does not understand.</para>
/// <para>This may be caused by the server's software version being older than the client was built against.</para>
/// <para>This can also be caused by the endpoint's hardware model not supporting the command.</para>
/// </summary>
public class CommandNotFoundException: ClientException {

    /// <summary>
    /// The space-separated name of the xAPI command that failed to execute when the disconnection occurred, such as <c>xCommand Dial</c>.
    /// </summary>
    public string CommandName { get; }

    /// <inheritdoc cref="CommandNotFoundException"/>
    /// <param name="hostname">The FQDN or IP address of the Cisco endpoint</param>
    /// <param name="path">list of elements in the command's path, such as <c>["xCommand", "Dial"]</c></param>
    /// <param name="cause">The underlying exception</param>
    public CommandNotFoundException(string hostname, object[] path, Exception cause): this(hostname, path.JoinPath(), cause) { }

    private CommandNotFoundException(string hostname, string name, Exception cause): base(
        $"xAPI command \"{name}\" was not found on the endpoint {hostname}, it may not be available on the software version or hardware of the endpoint", hostname, cause) {
        CommandName = name;
    }

}

/// <summary>
/// The client sent a parameter value that was not allowed by the server.
/// </summary>
public class IllegalArgumentException: ClientException {

    /// <summary>
    /// The space-separated name of the xAPI command that failed to execute when the disconnection occurred, such as <c>xCommand Dial</c>.
    /// </summary>
    public string CommandName { get; }

    /// <inheritdoc cref="IllegalArgumentException"/>
    /// <param name="hostname">The FQDN or IP address of the Cisco endpoint</param>
    /// <param name="path">list of elements in the command's path, such as <c>["xCommand", "Dial"]</c></param>
    /// <param name="arguments">the parameter values sent by the client</param>
    /// <param name="cause">The underlying exception</param>
    public IllegalArgumentException(string hostname, object[] path, IDictionary<string, object?>? arguments, Exception cause): this(hostname, path.JoinPath(), arguments, cause) { }

    /// <inheritdoc cref="IllegalArgumentException"/>
    /// <param name="hostname">The FQDN or IP address of the Cisco endpoint</param>
    /// <param name="path">list of elements in the command's path, such as <c>["xCommand", "Dial"]</c></param>
    /// <param name="argument">the parameter value sent by the client</param>
    /// <param name="cause">The underlying exception</param>
    public IllegalArgumentException(string hostname, object[] path, object argument, Exception cause):
        this(hostname, path.JoinPath(), new Dictionary<string, object?> { { "value", argument } }, cause) { }

    private IllegalArgumentException(string hostname, string path, IDictionary<string, object?>? arguments, Exception cause): base(
        $"Illegal argument passed to \"{path}\" on endpoint {hostname} {{ {(arguments == null ? "null" : string.Join(", ", arguments.Select(pair => $"{pair.Key}={pair.Value}")))} }}", hostname,
        cause) {
        CommandName = path;
    }

}

/// <summary>
/// <para>The client tried to log in to the endpoint with an incorrect <paramref name="username"/> or password.</para>
/// <para>It's also possible the <paramref name="hostname"/> was wrong, and the client connected to a different server that returned an HTTP status code that was not 101.</para>
/// </summary>
/// <param name="hostname">The FQDN or IP address of the Cisco endpoint</param>
/// <param name="username">The username that the client sent to the server</param>
/// <param name="cause">The underlying exception</param>
public class AuthenticationException(string hostname, string username, Exception cause)
    : ClientException($"Not authenticated with username {username}, or incorrect hostname, for WebSocket server {hostname}", hostname, cause) {

    /// <summary>
    /// The username that the client sent to the server
    /// </summary>
    public string Username { get; } = username;

}