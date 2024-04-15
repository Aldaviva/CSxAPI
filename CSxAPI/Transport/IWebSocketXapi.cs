using Newtonsoft.Json.Linq;

namespace CSxAPI.Transport;

public interface IWebSocketXapi: IXapiTransport, IWebSocketClient {

    Task<long> Subscribe(object[] path, bool notifyCurrentValue = false);

    Task<bool> Unsubscribe(long subscriptionId);

    delegate void SubscriptionPublishedArgs(long subscriptionId, JObject payload);
    public event SubscriptionPublishedArgs? SubscriptionPublished;

}