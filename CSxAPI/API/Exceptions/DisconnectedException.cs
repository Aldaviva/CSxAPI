namespace CSxAPI.API.Exceptions;

public class DisconnectedException: XapiException {

    public string CommandName { get; }

    public DisconnectedException(string hostname, object[] path, Exception cause): this(hostname, path.JoinPath(), cause) { }

    private DisconnectedException(string hostname, string path, Exception cause): base(
        $"Connection to endpoint {hostname} disconnected while running \"{path}\" command, and the {nameof(CSxAPIClient)} instance had {nameof(CSxAPIClient.AutoReconnect)} disabled", hostname,
        cause) {
        CommandName = path;
    }

}