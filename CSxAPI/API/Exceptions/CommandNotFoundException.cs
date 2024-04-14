namespace CSxAPI.API.Exceptions;

public class CommandNotFoundException: XapiException {

    public CommandNotFoundException(string hostname, string[] path, Exception cause): this(hostname, string.Join(' ', path), cause) { }

    private CommandNotFoundException(string hostname, string name, Exception cause): base(
        $"xAPI command \"{name}\" was not found on the endpoint {hostname}, it may not be available on the software version or hardware of the endpoint", hostname, cause) {
        CommandName = name;
    }

    public string CommandName { get; }

}