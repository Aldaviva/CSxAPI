namespace CSxAPI.API.Exceptions;

public abstract class XapiException(string message, string hostname, Exception cause): ApplicationException(message, cause) {

    public string Hostname { get; init; } = hostname;

}