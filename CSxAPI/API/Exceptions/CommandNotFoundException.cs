namespace CSxAPI.API.Exceptions;

public class CommandNotFoundException: XapiException {

    public string CommandName { get; }

    public CommandNotFoundException(string hostname, object[] path, Exception cause): this(hostname, path.JoinPath(), cause) { }

    private CommandNotFoundException(string hostname, string name, Exception cause): base(
        $"xAPI command \"{name}\" was not found on the endpoint {hostname}, it may not be available on the software version or hardware of the endpoint", hostname, cause) {
        CommandName = name;
    }

}