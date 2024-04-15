using CSxAPI.API.Exceptions;

namespace CSxAPI.Transport;

/// <summary>
/// Connects to a Cisco collaboration endpoint's xAPI
/// </summary>
public interface IXapiTransport: IDisposable, IAsyncDisposable {

    /// <summary>
    /// The hostname (IP address or FQDN) of the endpoint on the network
    /// </summary>
    public string Hostname { get; }

    /// <summary>
    /// The username with which to authenticate to the endpoint, such as <c>admin</c>
    /// </summary>
    public string Username { get; }

    /// <summary>Read the value of an xConfiguration or xStatus</summary>
    /// <param name="path">The name of the configuration or status, such as <c>["xConfiguration", "SystemUnit", "Name"]</c> or <c>["xStatus", "SystemUnit", "Uptime"]</c></param>
    /// <returns>The current value of the configuration or status on the endpoint</returns>
    /// <exception cref="CommandNotFoundException">The endpoint software version or hardware does not support this configuration or status</exception>
    Task<T> GetConfigurationOrStatus<T>(object[] path);

    /// <summary>Write the value of an xConfiguration</summary>
    /// <param name="path">The name of the configuration, such as <c>["xConfiguration", "SystemUnit", "Name"]</c></param>
    /// <param name="newValue">The value to set on the endpoint</param>
    /// <exception cref="CommandNotFoundException">The endpoint software version or hardware does not support this configuration</exception>
    Task SetConfiguration(object[] path, object newValue);

    /// <summary>Invoke an xCommand</summary>
    /// <param name="path">The name of the command, such as <c>["xCommand", "Dial"]</c></param>
    /// <param name="parameters">Map of command parameter names and their values</param>
    /// <returns>Map of key-value pairs returned by the command</returns>
    /// <exception cref="CommandNotFoundException">The endpoint software version or hardware does not support this command</exception>
    Task<IDictionary<string, object>> CallMethod(object[] path, IDictionary<string, object?>? parameters);

}