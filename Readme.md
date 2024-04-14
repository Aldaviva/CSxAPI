CSxAPI
===

![target version](https://img.shields.io/badge/target%20version-RoomOS%2011.5-%3B?logo=cisco&logoColor=white) [![NuGet](https://img.shields.io/nuget/v/CSxAPI?logo=nuget)](https://www.nuget.org/packages/csxapi)

CSxAPI is a strongly-typed API client library for Cisco **xAPI**. It is similar to the official [jsxapi](https://www.npmjs.com/package/jsxapi) implementation, but for .NET instead of JavaScript. xAPI is exposed by [Cisco video conferencing devices](https://www.cisco.com/c/en/us/products/collaboration-endpoints/collaboration-room-endpoints/index.html), which are known by many names:

- Collaboration Endpoint
- Webex Room
- Cisco device
- Room system
- TelePresence
- Codec

This library can send and receive xCommands, xConfigurations, xStatuses, and xEvents over a WebSocket connection, which is available in Cisco software versions ≥ CE 9.7, and enabled by default in versions ≥ RoomOS 10.8.

----

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
    1. [Dependent unit testing](#dependent-unit-testing)

<!-- /MarkdownTOC -->

![Room Kit](https://raw.githubusercontent.com/Aldaviva/CSxAPI/master/.github/images/readme-header.jpg)


## Requirements
- [.NET 6 or later](https://dotnet.microsoft.com/en-us/download/dotnet)
- [Cisco endpoint](https://www.cisco.com/c/en/us/products/collaboration-endpoints/collaboration-room-endpoints/index.html)
    - *Targeted endpoint software version:* **RoomOS 11.5**
        - Each CSxAPI library release targets exactly one endpoint software version, ideally the latest on-premises version
        - Other endpoint software versions should also work, as long as the API didn't introduce any breaking changes from the target version
        - This library makes no additional attempt at backwards compatibility other than that afforded by xAPI, which is very backwards compatible on its own
    - *Minimum endpoint software versions:* CE 9.7 and RoomOS 10.3
    - *Hardware:* Room, Board, Desk, SX, DX, or MX series are compatible
        - Tested on a Room Kit and a Room Kit Plus PTZ 4K
        - xAPI does not exist on C, CTS, E, EX, IX, MXP, or T series endpoints, therefore they are not compatible
    - *Configuration:* WebSocket protocol must be enabled
        - Enable by running `xConfiguration NetworkServices Websocket: FollowHTTPService` through SSH, Telnet, or an RS-232 serial connection (XACLI); the web admin site; or the XML HTTP API (TXAS)
        - Enabled by default in versions ≥ RoomOS 10.8
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
For xAPI documentation, refer to the [API Reference Guide PDF](https://www.cisco.com/c/en/us/support/collaboration-endpoints/spark-room-kit-series/products-command-reference-list.html) for the endpoint software version that this CSxAPI release targets, RoomOS 11.5.

Alternatively, you may refer to the [online xAPI documentation site](https://roomos.cisco.com/xapi).

## Connection
```cs
using CSxAPI;

await using XAPI xAPI = new CSxAPIClient(hostname: "192.168.1.100", username: "admin", password: "password123!");

await xAPI.Connect();
```

To disconnect, `CSxAPIClient` must be disposed with `await using`, `using`, or `.Dispose()`.

#### Options

You don't have to pass any of these options, but they're here if you need them.

```cs
new CSxAPIClient(hostname, username, password) {
    AllowSelfSignedTls = false,
    ConsoleTracing = false
};
```

- **`AllowSelfSignedTls`:** set to `true` if connections to WebSocket servers with self-signed or invalid TLS certificates should be allowed, or `false` (default) to require valid certificates that chain to trusted CAs.
    - If you want a valid TLS certificate for your Cisco endpoint, you may consider using [Let's Encrypt](https://letsencrypt.org) and [Aldaviva/CiscoEndpointCertificateDeployer](https://github.com/Aldaviva/CiscoEndpointCertificateDeployer).
- **`ConsoleTracing`:** set to `true` to print all JSON-RPC requests and responses sent and received over the WebSocket connection to the console.

## Operations

### Commands
```cs
IDictionary<string, object> result = await xapi.Command.Dial(number: "10990@bjn.vc");
Console.WriteLine($"Dialed call {result["CallId"]} (conference {result["ConferenceId"]})");
```

### Configurations

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
```cs
xapi.Event.UserInterface.Message.TextInput.Response += response => {
    if (response.FeedbackId == "my expected feedback ID") {
        Console.WriteLine($"User entered {response.Text} into the TextInput dialog");
    }
};
```

## Error handling
#### Disconnections
The `XAPI` interface exposes the `bool IsConnected` property, which is `true` when the WebSocket is connected and `false` otherwise.

To receive notifications when it is disconnected, subscribe to the `Disconnected` event.

#### Reconnection
Not yet implemented

#### Method not found
Not yet implemented

#### Illegal arguments
Not yet implemented

## Testing
### Dependent unit testing
To do 