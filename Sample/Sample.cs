using CSxAPI;

CancellationTokenSource exit = new();
Console.CancelKeyPress += (_, eventArgs) => {
    eventArgs.Cancel = true;
    exit.Cancel();
};

Console.WriteLine("Connecting...");
await using XAPI xapi = await new CSxAPIClient("whiterazor.aldaviva.com", "ben", Environment.GetEnvironmentVariable("password") ?? "") { ConsoleTracing = false }
    .Connect();

await ReadEndpointName();
await ReadUptime();
// await Dial();

Console.WriteLine("Press Ctrl+C to hang up and exit.");
exit.Token.WaitHandle.WaitOne();

await xapi.Command.Call.Disconnect();

async Task ReadEndpointName() {
    string name = await xapi.Configuration.SystemUnit.Name();
    Console.WriteLine($"Endpoint name: {name}");
}

async Task ReadUptime() {
    TimeSpan uptime = TimeSpan.FromSeconds(await xapi.Status.SystemUnit.Uptime());
    Console.WriteLine($"Endpoint has been running for {uptime.Days:N0} day(s), {uptime.Hours:N0} hour(s), {uptime.Minutes:N0} minute(s), and {uptime.Seconds:N0} second(s).");
}

async Task Dial() {
    IDictionary<string, object> result = await xapi.Command.Dial(number: "10990@bjn.vc");
    Console.WriteLine($"Dialed call ID {result["CallId"]} (conference ID {result["ConferenceId"]}).");
}