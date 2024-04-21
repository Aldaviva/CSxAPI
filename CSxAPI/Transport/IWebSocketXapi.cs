using Newtonsoft.Json.Linq;

namespace CSxAPI.Transport;

/// <summary>
/// A client interface for WebSocket connections to Cisco xAPI.
/// </summary>
public interface IWebSocketXapi: IXapiTransport, IWebSocketClient {

    /// <summary>
    /// Register for notifications to feedback events.
    /// </summary>
    /// <param name="path">feedback topic, such as <c>["xEvent", "Audio", "MicrophonesMuteStatus"]</c></param>
    /// <param name="notifyCurrentValue"><c>true</c> to immediately retrieve the current value and fire an event, or <c>false</c> to not do that. Either way, the subscriber will still be registered for all future events.</param>
    /// <returns>numeric subscription ID to use when receiving events and unsubscribing</returns>
    Task<long> Subscribe(object[] path, bool notifyCurrentValue = false);

    /// <summary>
    /// Unregister from future events on an existing feedback subscription.
    /// </summary>
    /// <param name="subscriptionId">The numeric subscription ID returned by <see cref="Subscribe"/>.</param>
    Task Unsubscribe(long subscriptionId);

    /// <summary>
    /// Signature of a method to run when a feedback event is received from the endpoint and its callback must be called
    /// </summary>
    /// <param name="subscriptionId">The numeric ID of the subscription, returned when first subscribing to the event</param>
    /// <param name="payload">The JSON object sent by the endpoint in the event</param>
    delegate void SubscriptionPublishedArgs(long subscriptionId, JObject payload);

    /// <summary>
    /// A feedback event was sent from the Cisco endpoint to this library, and now the user-supplied callback must be run
    /// </summary>
    public event SubscriptionPublishedArgs? SubscriptionPublished;

}