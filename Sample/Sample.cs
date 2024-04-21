using CSxAPI;

Console.WriteLine("Connecting...");
await using XAPI xapi = new CSxAPIClient("whisperblade.aldaviva.com", "ben", Environment.GetEnvironmentVariable("password") ?? "") { ConsoleTracing = false, AllowSelfSignedTls = false };
xapi.IsConnectedChanged += (connected, _) => Console.WriteLine(connected ? "Connected" : "Disconnected");
await xapi.Connect();

Task<string> name            = xapi.Configuration.SystemUnit.Name();
Task<string> modelName       = xapi.Status.SystemUnit.ProductId();
Task<string> softwareVersion = xapi.Status.SystemUnit.Software.DisplayName();
Task<string> softwareDate    = xapi.Status.SystemUnit.Software.ReleaseDate();
Console.WriteLine("Endpoint name: {0} ({1}, {2} {3})", await name, await modelName, await softwareVersion, await softwareDate);

TimeSpan uptime = TimeSpan.FromSeconds(await xapi.Status.SystemUnit.Uptime());
Console.WriteLine($"Uptime: {uptime.Days:N0} day(s), {uptime.Hours:N0} hour(s), {uptime.Minutes:N0} minute(s), and {uptime.Seconds:N0} second(s).");

for (int serverIndex = 1; serverIndex <= 3; serverIndex++) {
    string dnsServer = await xapi.Configuration.Network.N.DNS.Server.N.Address(1, serverIndex);
    Console.WriteLine($"DNS Server {serverIndex:N0}: {dnsServer}");
}

static void PrintAudioVolume(int value) => Console.WriteLine($"Audio output volume: {value} dB");
PrintAudioVolume(await xapi.Status.Audio.Volume());
xapi.Status.Audio.VolumeChanged += PrintAudioVolume;

Console.WriteLine("\nPress Enter to show current time on endpoint.");
Console.WriteLine("Press Ctrl+C to exit.");

Console.TreatControlCAsInput = true;
while (true) {
    ConsoleKeyInfo keyInfo = Console.ReadKey(true);
    if (keyInfo.Key == ConsoleKey.Enter) {
        try {
            string         rawTime = await xapi.Status.Time.SystemTime();
            DateTimeOffset time    = DateTimeOffset.Parse(rawTime);
            Console.WriteLine($"Endpoint time: {time:F}");
        } catch (Exception e) when (e is not OutOfMemoryException) {
            Console.WriteLine(e);
        }
    } else if (keyInfo is { Key: ConsoleKey.C, Modifiers: ConsoleModifiers.Control }) {
        Console.WriteLine("Got Ctrl+C hotkey");
        break;
    }
}