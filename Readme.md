CSxAPI
===

![target version](https://img.shields.io/badge/target%20version-RoomOS%2011.14-%3B?logo=cisco&logoColor=white) [![NuGet](https://img.shields.io/nuget/v/CSxAPI?logo=nuget)](https://www.nuget.org/packages/csxapi)

CSxAPI is a strongly-typed API client library for Cisco **xAPI**. It is similar to the official [jsxapi](https://www.npmjs.com/package/jsxapi) implementation, but for .NET instead of JavaScript. xAPI is an API exposed by [Cisco video conferencing devices](https://www.cisco.com/c/en/us/products/collaboration-endpoints/collaboration-room-endpoints/index.html), also known as Collaboration Endpoints, Webex Rooms, Cisco devices, room systems, TelePresence, and codecs.

This library can send and receive xCommands, xConfigurations, xStatuses, and xEvents over a WebSocket connection, which is available in Cisco software versions ≥ CE 9.7, and enabled by default in versions ≥ RoomOS 10.8.

<!-- MarkdownTOC autolink="true" bracket="round" autoanchor="false" levels="1,2,3" bullets="1." -->

1. [Requirements](#requirements)
1. [Installation](#installation)
1. [API documentation](#api-documentation)
1. [Connection](#connection)
1. [Operations](#operations)
    1. [Commands](#commands)
    1. [Configurations](#configurations)
    1. [Statuses](#statuses)
    1. [Events](#events)
1. [Error handling](#error-handling)
1. [Testing](#testing)

<!-- /MarkdownTOC -->

![Room Kit](https://raw.githubusercontent.com/Aldaviva/CSxAPI/master/.github/images/readme-header.jpg)


## Requirements
- [.NET 6 or later](https://dotnet.microsoft.com/en-us/download/dotnet)
- [Cisco endpoint](https://www.cisco.com/c/en/us/products/collaboration-endpoints/collaboration-room-endpoints/index.html)
    - *Targeted endpoint software version:* **RoomOS 11.14**
        - Each CSxAPI library release targets exactly one endpoint software version, ideally the latest on-premises version
        - Other endpoint software versions should also work, as long as the API didn't introduce any breaking changes from the target version
        - This library makes no additional attempt at backwards compatibility other than that afforded by xAPI, which is very backwards compatible on its own
    - *Minimum endpoint software versions:* CE 9.7 and RoomOS 10.3
    - *Hardware:* Room, Board, Desk, SX, DX, or MX series are compatible
        - Tested on a Room Kit and a Room Kit Plus PTZ 4K
        - WebSocket xAPI does not exist on C, CTS, E, EX, IX, MXP, or T series endpoints, therefore they are not compatible
    - *Configuration:* WebSocket protocol must be enabled
        - Enabled by default in endpoint software versions ≥ RoomOS 10.8
        - Enable by running `xConfiguration NetworkServices Websocket: FollowHTTPService` through SSH, Telnet, or an RS-232 serial connection (XACLI); the web admin site; or the XML HTTP API (TXAS)
        - `/Configuration/NetworkServices/HTTP/Mode` must not be `Off`
    - *Addressing:* you must know the endpoint's IP address, FQDN, or other hostname
    - *Authentication:* you need the username and password of an active user that can log in to the endpoint
        - If the endpoint is registered to [Webex](https://admin.webex.com/devices/search) and has no local users, you must create a new local user through Local Device Controls
    - *Network:* open TCP route from your client to port 443 on the endpoint

## Installation
The [**`CSxAPI`** package](https://www.nuget.org/packages/csxapi) is available on NuGet.

```ps1
dotnet add package CSxAPI
```

## API documentation
For xAPI documentation, refer to the [API Reference Guide PDF](https://www.cisco.com/c/en/us/support/collaboration-endpoints/spark-room-kit-series/products-command-reference-list.html) for the endpoint software version that this CSxAPI release targets.

Alternatively, you may refer to the [online xAPI documentation](https://roomos.cisco.com/xapi).

## Connection
```cs
using CSxAPI;

await using XAPI xAPI = new CSxAPIClient(hostname: "192.168.1.100", username: "admin", password: "password123!");

await xAPI.Connect();
```

To disconnect, `CSxAPIClient` instances must be disposed with `await using`, `using`, or `.Dispose()`.

#### Options

You don't have to pass any of these options, but they're here if you need them.

```cs
new CSxAPIClient(hostname, username, password) {
    AllowSelfSignedTls = false,
    AutoReconnect = true,
    ConsoleTracing = false
};
```

- **`AllowSelfSignedTls`:** set to `true` if connections to WebSocket servers with self-signed or invalid TLS certificates should be allowed, or `false` (default) to require valid certificates that chain to trusted CAs.
    - If you want a valid TLS certificate for your Cisco endpoint, you may consider using [Let's Encrypt](https://letsencrypt.org) and [Aldaviva/CiscoEndpointCertificateDeployer](https://github.com/Aldaviva/CiscoEndpointCertificateDeployer).
- **`AutoReconnect`:** set to `false` to disable [automatic reconnections](#reconnections) when the WebSocket is lost
- **`ConsoleTracing`:** set to `true` to print all JSON-RPC requests and responses sent and received over the WebSocket connection to the console.

## Operations

### Commands

You can call `xCommand` methods, passing arguments and receiving a dictionary response.

```cs
IDictionary<string, object> result = await xapi.Command.Dial(number: "10990@bjn.vc");
Console.WriteLine($"Dialed call {result["CallId"]} (conference {result["ConferenceId"]})");
```

### Configurations

You can read and write the value of `xConfiguration` options.

#### Get
```cs
string name = await xapi.Configuration.SystemUnit.Name();
```

#### Set
```cs
await xapi.Configuration.SystemUnit.Name("Whisper Blade");
```

#### Notify
```cs
xapi.Configuration.SystemUnit.NameChanged += newName => Console.WriteLine($"System name changed to {newName}");
```

### Statuses

You can read the current value of `xStatus` states.

#### Get
```cs
TimeSpan uptime = TimeSpan.FromSeconds(await xapi.Status.SystemUnit.Uptime());
Console.WriteLine($"Endpoint has been up for {uptime.Days:N0} day(s), {uptime.Hours:N0} hour(s), {uptime.Minutes:N0} minute(s), and {uptime.Seconds:N0} second(s).");
```

#### Notify
```cs
xapi.Status.Call.N.StatusChanged += callStatus => {
    if (callStatus == StatusCallNStatus.Connected) {
        Console.WriteLine("Call connected");
    }
};
```

### Events

Aside from [changes to Configuration](#notify) and [Status](#notify-1), you can also listen for an `xEvent` being emitted.

```cs
xapi.Event.UserInterface.Message.TextInput.Response += response => {
    if (response.FeedbackId == "my expected feedback ID") {
        Console.WriteLine($"User entered {response.Text} into the TextInput dialog");
    }
};
```

## Error handling
#### Disconnections
The `CSxAPIClient` class exposes the `bool IsConnected` property, which is `true` when the WebSocket is connected and `false` otherwise.

To receive notifications when it is disconnected, subscribe to the `IsConnectedChanged` event. Its `isConnected` argument will be `false` if the endpoint just disconnected, or `true` if it just [reconnected](#reconnections). The `disconnectionDetails` will contain the underlying reason behind the disconnection if the endpoint just disconnected, otherwise it will be `null` if the endpoint just reconnected. This event is fired when the initial call to `Connect()` succeeds, but not when the `CSxAPIClient` instance is disposed.

#### Reconnections
The WebSocket connection between this library and the endpoint can disconnect, for example, if an ethernet cable is unplugged, or if the endpoint reboots. When this happens, CSxAPI will try to automatically reconnect.

If it succeeds, it will fire the `IsConnectedChanged` event with `isConnected` set to `true`, calls to xCommands, xConfigurations, and xStatuses will succeed again, and any xFeedback events to which you previously subscribed will be able to be received again. Any commands that were sent while the endpoint was reconnecting will automatically wait for the connection is reestablished, retry, and return the new result to the original caller.

To disable automatic reconnections, set the `AutoReconnect` property to `false` on the `CSxAPIClient`. In this case, any commands that are sent while the endpoint is disconnected will throw a `DisconnectedException` instead of being retried:

> `CSxAPI.API.Exceptions.DisconnectedException: Connection to endpoint whiterazor.aldaviva.com disconnected while running "xCommand Dial" command, and the CSxAPIClient instance had AutoReconnect disabled`

#### Method not found
If you try to call an xCommand, xConfiguration, or xStatus on an endpoint that does not support it, the method call will asynchronously throw a `CommandNotFoundException`.

For example, `xCommand Cameras Background Clear` only applies to Desk series endpoints. If you try to call it on a different endpoint, such as a Room Kit, it will throw that exception.

> `CSxAPI.API.Exceptions.CommandNotFoundException: xAPI command "xCommand Cameras Background Clear" was not found on the endpoint whiterazor.aldaviva.com, it may not be available on the software version or hardware of the endpoint`

#### Illegal arguments
If you try to call an xCommand or xConfiguration on an endpoint and pass an argument that is invalid, the method will asynchronously throw an `IllegalArgumentException`.

For example, `xCommand Dial` requires a `Number` argument. If you pass the empty string, instead of a valid dial string like a SIP URI, it will throw that exception.

> `CSxAPI.API.Exceptions.IllegalArgumentException: Illegal argument passed to "xCommand Dial" on endpoint whiterazor.aldaviva.com { Number=, Protocol=, CallRate=, CallType=, BookingId=, Appearance=, DisplayName=, TrackingData= }`

## Testing
If you want to automate testing of code that depends on CSxAPI, you can easily mock out CSxAPI because it is based on interfaces like `XAPI`.

For example, using the best-in-class .NET testing libraries [FakeItEasy](https://fakeiteasy.github.io/docs/) for mocking and [Fluent Assertions](https://fluentassertions.com/introduction) for assertions, it's simple to verify this [sample code under test](https://github.com/Aldaviva/CSxAPI/blob/master/Tests/SampleUnitTest.cs) that dials and hangs up a call.

```cs
using CSxAPI;
using FakeItEasy;
using FluentAssertions;
using Xunit;

public class SampleUnitTest {

    private readonly XAPI _xapi = A.Fake<XAPI>(); // mocked CSxAPIClient

    [Fact]
    public async Task DialAndHangUp() {
        // Arrange
        A.CallTo(() => _xapi.Command.Dial(A<string>._, null, null, null, null, null, null, null))
            .Returns(new Dictionary<string, object> { ["CallId"] = 3, ["ConferenceId"] = 2 });

        // Act
        IDictionary<string, object> actual = await _xapi.Command.Dial("10990@bjn.vc");
        await _xapi.Command.Call.Disconnect((int?) actual["CallId"]);

        // Assert
        actual["CallId"].Should().Be(3);
        actual["ConferenceId"].Should().Be(2);

        A.CallTo(() => _xapi.Command.Dial("10990@bjn.vc", null, null, null, null, null, null, null))
            .MustHaveHappened();
        A.CallTo(() => _xapi.Command.Call.Disconnect(3)).MustHaveHappened();
    }

}
```