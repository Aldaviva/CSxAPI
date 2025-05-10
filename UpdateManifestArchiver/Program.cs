using Bom.Squad;
using DataSizeUnits;
using McMaster.Extensions.CommandLineUtils;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Unfucked;
using Unfucked.HTTP;
using UpdateManifestArchiver;

BomSquad.DefuseUtf8Bom();

CancellationTokenSource cts = new CancellationTokenSource().CancelOnCtrlC();

CommandLineApplication app = new();
app.Conventions.UseDefaultConventions();
app.VersionOptionFromAssemblyAttributes(typeof(Program).Assembly);
app.Description      = "Downloads the current Cisco Webex RoomOS endpoint software update manifest JSON file to disk.";
app.ExtendedHelpText = $"\nExample: {app.Name} --overwrite \"E:\\Applications\\Cisco\\Endpoints\"";

CommandArgument<string> destinationDirectory = app.Argument<string>("destinationDirectory", "Directory in which to save update manifest JSON files.").IsRequired();
CommandOption<bool>     overwrite            = app.Option<bool>("-o|--overwrite", "Replace any existing manifest file with the same minor version.", CommandOptionType.NoValue);

bool exit = true;
app.OnExecute(() => exit = false);
app.Execute(args);
if (exit) return 1;

using UnfuckedHttpClient httpClient = new();

bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

try {
    Console.WriteLine($"Downloading update manifest from Webex API ({WebexApi.GetUpdateManifest.AbsoluteUri})");
    var        manifestBytes = await httpClient.Target(WebexApi.GetUpdateManifest).Get<ReadOnlyMemory<byte>>(cts.Token);
    JsonObject manifestJson  = JsonSerializer.Deserialize<JsonObject>(manifestBytes.Span)!;

    if (manifestJson["manifest"]?["version"]?.GetValue<string>() is not { } versionText) {
        Console.WriteLine("Could not find version text in manifest JSON file");
        return 1;
    }

    if (versionPattern().Match(versionText) is not { Success: true } versionMatch) {
        Console.WriteLine($"Could not extract version number from text {versionText}");
        return 1;
    }

    string   version     = versionMatch.Groups["versionNumber"].Value;
    DateOnly buildDate   = DateOnly.Parse(versionMatch.Groups["date"].Value);
    DateTime releaseDate = manifestJson["createdAt"]!.GetValue<DateTime>();
    Console.WriteLine(
        $"Current stable software version is RoomOS {version} (commit {versionMatch.Groups["commitHash"].Value}), built on {buildDate:D} and released on {releaseDate:D} ({(DateTime.Now - releaseDate).humanize()}).");

    string     destinationFilePath = Path.GetFullPath(Path.Combine(Environment.ExpandEnvironmentVariables(destinationDirectory.ParsedValue), $"webex binaries {version}.json"));
    FileStream jsonFileStream;

    try {
        jsonFileStream = File.Open(destinationFilePath, overwrite.ParsedValue ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
    } catch (IOException e) when (!overwrite.ParsedValue && (isWindows ? (ushort) e.HResult == 80 : File.Exists(destinationFilePath))) {
        Console.WriteLine($"Not overwriting existing file '{destinationFilePath}' (pass --{overwrite.LongName} to replace it)");
        return 0;
    }

    await using (jsonFileStream) {
        await jsonFileStream.WriteAsync(manifestBytes, cts.Token);
    }

    Console.WriteLine($"Saved \"{destinationFilePath}\" ({new DataSize(manifestBytes.Length).ToString(2, true)}).");
    return 0;
} catch (HttpRequestException e) {
    Console.WriteLine($"HTTP request to Webex API failed: {e.Message}");
} catch (TaskCanceledException) {
    Console.WriteLine("HTTP request to Webex API timed out");
} catch (JsonException e) {
    Console.WriteLine($"Webex API returned corrupted JSON: {e.Message}");
}

return 1;

internal abstract partial class Program {

    [GeneratedRegex(@"[a-z]*(?<versionNumber>\d+(?:\.\d+)+) (?<commitHash>[a-f0-9]{11}) (?<date>\d{4}-\d{2}-\d{2})", RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex versionPattern();

}