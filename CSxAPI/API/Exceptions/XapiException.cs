namespace CSxAPI.API.Exceptions;

public abstract class XapiException: ApplicationException {

    protected XapiException(string message, string hostname, Exception cause): base(message, cause) {
        Hostname = hostname;
    }

    public string Hostname { get; init; }

}