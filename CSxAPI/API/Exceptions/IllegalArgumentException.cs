namespace CSxAPI.API.Exceptions;

public class IllegalArgumentException: XapiException {

    public IllegalArgumentException(string hostname, string[] path, IDictionary<string, object?>? arguments, Exception cause): this(hostname, string.Join(' ', path), arguments, cause) { }

    private IllegalArgumentException(string hostname, string path, IDictionary<string, object?>? arguments, Exception cause): base(
        $"Illegal argument passed to \"{path}\" on endpoint {hostname} {{ {(arguments == null ? "null" : string.Join(", ", arguments.Select(pair => $"{pair.Key}={pair.Value}")))} }}", hostname,
        cause) { }

    public IllegalArgumentException(string hostname, string[] path, object argument, Exception cause):
        this(hostname, string.Join(' ', path), new Dictionary<string, object?> { { "value", argument } }, cause) { }

}