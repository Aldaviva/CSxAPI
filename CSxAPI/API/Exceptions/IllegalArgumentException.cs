namespace CSxAPI.API.Exceptions;

public class IllegalArgumentException: XapiException {

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