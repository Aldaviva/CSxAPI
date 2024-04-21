using CSxAPI.Transport;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;
using System.Collections.Concurrent;

namespace CSxAPI.API;

/// <summary>
/// Keep track of which numeric subscription ID (from Cisco) belongs to which callback (from the library consumer).
/// </summary>
public interface IFeedbackSubscriber: IDisposable {

    /// <summary>
    /// How long to wait to subscribe to a feedback event
    /// </summary>
    TimeSpan Timeout { get; set; }

    /// <summary>
    /// Subscribe to a feedback event for <c>xEvent</c>, <c>xConfiguration</c>, or <c>xStatus</c> notifications.
    /// </summary>
    /// <typeparam name="TSerialized">Type returned by the JSON WebSocket response, either <see cref="int"/> or <see cref="string"/></typeparam>
    /// <typeparam name="TDeserialized">Type of the value passed to the <paramref name="callback"/>, such as <see cref="int"/>, <see cref="string"/>, or an <see cref="Enum"/>.</typeparam>
    /// <param name="path">Path of the feedback subscription, such as <c>[ "xConfiguration", "Audio", "DefaultVolume" ]</c></param>
    /// <param name="callback">Run when the event is fired</param>
    /// <param name="deserialize">Convert the serialized JSON data into the parameter to <paramref name="callback"/></param>
    /// <param name="notifyCurrentValue"><c>true</c> to also fire an event with the current value, or <c>false</c> to not do that. Either way, future events will be subscribed to.</param>
    Task Subscribe<TSerialized, TDeserialized>(object[] path, FeedbackCallback<TDeserialized> callback, Func<TSerialized, TDeserialized> deserialize,
                                               bool     notifyCurrentValue = false);

    /// <summary>
    /// Subscribe to a feedback event for <c>xEvent</c>, <c>xConfiguration</c>, or <c>xStatus</c> notifications.
    /// </summary>
    /// <typeparam name="TDeserialized">Type of the value passed to the <paramref name="callback"/>, such as <see cref="int"/>, <see cref="string"/>, or an <see cref="Enum"/>.</typeparam>
    /// <param name="path">Path of the feedback subscription, such as <c>[ "xConfiguration", "Audio", "DefaultVolume" ]</c></param>
    /// <param name="callback">Run when the event is fired</param>
    /// <param name="deserialize">Convert the serialized JSON data into the parameter to <paramref name="callback"/></param>
    /// <param name="notifyCurrentValue"><c>true</c> to also fire an event with the current value, or <c>false</c> to not do that. Either way, future events will be subscribed to.</param>
    Task Subscribe<TDeserialized>(object[] path, FeedbackCallback<TDeserialized> callback, Func<JObject, TDeserialized> deserialize, bool notifyCurrentValue = false);

    /// <summary>
    /// Subscribe to a feedback event for <c>xEvent</c>, <c>xConfiguration</c>, or <c>xStatus</c> notifications.
    /// </summary>
    /// <param name="path">Path of the feedback subscription, such as <c>[ "xConfiguration", "Audio", "DefaultVolume" ]</c></param>
    /// <param name="callback">Run when the event is fired</param>
    Task Subscribe(object[] path, FeedbackCallback callback);

    /// <summary>
    /// Deregister from an existing feedback subscription so no more events from it will be fired.
    /// </summary>
    /// <typeparam name="T">Type of the value that had been passed to the <paramref name="callback"/></typeparam>
    /// <param name="callback">Run when the event was fired</param>
    Task Unsubscribe<T>(FeedbackCallback<T> callback);

    /// <summary>
    /// Deregister from an existing feedback subscription so no more events from it will be fired.
    /// </summary>
    /// <param name="callback">Run when the event was fired</param>
    Task Unsubscribe(FeedbackCallback callback);

}

/// <inheritdoc cref="IFeedbackSubscriber"/>>
internal class FeedbackSubscriber: IFeedbackSubscriber {

    private readonly ConcurrentDictionary<long, Subscription> _subscribers = new(); // key is FeedbackCallback<T>, value is subscription ID

    private readonly IWebSocketXapi _transport;
    private readonly JsonSerializer _jsonSerializer = JsonSerializer.CreateDefault();

    private bool _reconnecting;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    public FeedbackSubscriber(IWebSocketXapi transport) {
        _transport = transport;

        transport.SubscriptionPublished += OnSubscriptionPublished;
        transport.IsConnectedChanged    += OnTransportConnectionChanged;
    }

    private void OnTransportConnectionChanged(bool isConnected, JsonRpcDisconnectedEventArgs? disconnectionDetails) {
        if (_reconnecting && isConnected) {
            ResubscribeAll().Wait();
        }

        _reconnecting = !isConnected;
    }

    private void OnSubscriptionPublished(long subscriptionId, JObject payload) {
        if (_subscribers.TryGetValue(subscriptionId, out Subscription? subscription)) {
            subscription.Handler(payload);
        } else {
            Console.WriteLine($"Missing handler for subscription {subscriptionId}");
        }
    }

    public async Task Subscribe<TSerialized, TDeserialized>(object[] path, FeedbackCallback<TDeserialized> callback, Func<TSerialized, TDeserialized> deserialize,
                                                            bool     notifyCurrentValue = false) {
        long subscriptionId = await _transport.Subscribe(path, notifyCurrentValue).ConfigureAwait(false);
        _subscribers[subscriptionId] = new Subscription(path, serialized => callback(deserialize(FindFirstLeaf(serialized).ToObject<TSerialized>(_jsonSerializer)!)), callback);
    }

    public async Task Subscribe<TDeserialized>(object[] path, FeedbackCallback<TDeserialized> callback, Func<JObject, TDeserialized> deserialize, bool notifyCurrentValue = false) {
        long subscriptionId = await _transport.Subscribe(path, notifyCurrentValue).ConfigureAwait(false);
        _subscribers[subscriptionId] = new Subscription(path, serialized => callback(deserialize(serialized)), callback);
    }

    public async Task Subscribe(object[] path, FeedbackCallback callback) {
        long subscriptionId = await _transport.Subscribe(path).ConfigureAwait(false);
        _subscribers[subscriptionId] = new Subscription(path, _ => callback(), callback);
    }

    public async Task Unsubscribe<T>(FeedbackCallback<T> callback) {
        KeyValuePair<long, Subscription>? subscription = _subscribers.FirstOrDefault(pair => callback == (FeedbackCallback<T>) pair.Value.Callback);
        if (subscription.HasValue && _subscribers.TryRemove(subscription.Value.Key, out Subscription? _)) {
            await _transport.Unsubscribe(subscription.Value.Key).ConfigureAwait(false);
        }
    }

    public async Task Unsubscribe(FeedbackCallback callback) {
        KeyValuePair<long, Subscription>? subscription = _subscribers.FirstOrDefault(pair => callback == (FeedbackCallback) pair.Value.Callback);
        if (subscription.HasValue && _subscribers.TryRemove(subscription.Value.Key, out Subscription? _)) {
            await _transport.Unsubscribe(subscription.Value.Key).ConfigureAwait(false);
        }
    }

    private async Task ResubscribeAll() {
        var oldSubscriptions = new Dictionary<long, Subscription>(_subscribers);
        _subscribers.Clear();

        foreach (Subscription oldSubscription in oldSubscriptions.Values) {
            long subscriptionId = await _transport.Subscribe(oldSubscription.Path).ConfigureAwait(false);
            _subscribers[subscriptionId] = oldSubscription;
        }
    }

    private static JToken FindFirstLeaf(JToken root) {
        while (root.HasValues) {
            root = root.First!;
        }

        return root;
    }

    private record Subscription(
        object[]        Path,
        Action<JObject> Handler,
        object          Callback
    );

    public void Dispose() {
        _transport.SubscriptionPublished -= OnSubscriptionPublished;
        _transport.IsConnectedChanged    -= OnTransportConnectionChanged;
        GC.SuppressFinalize(this);
    }

}

/// <summary>
/// The signature of a method used to subscribe to notifications from xAPI, such as <c>xFeedback</c> events.
/// </summary>
public delegate void FeedbackCallback();

/// <summary>
/// The signature of a method used to subscribe to notifications from xAPI, such as <c>xFeedback</c> events or changes to <c>xConfiguration</c> or <c>xStatus</c> values.
/// </summary>
public delegate void FeedbackCallback<in T>(T newValue);