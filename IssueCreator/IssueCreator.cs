using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using McMaster.Extensions.CommandLineUtils;
using Octokit;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Unfucked;

namespace IssueCreator;

internal partial class IssueCreator {

    private const string RepositoryOwner      = "Aldaviva";
    private const string RepositoryName       = "CSxAPI";
    private const string IssueLabel           = "upstream update";
    private const string UserAgentHeaderValue = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

    private static readonly AssemblyName   CurrentAssembly               = Assembly.GetExecutingAssembly().GetName();
    private static readonly Url            DocumentationListPageLocation = new("https://www.cisco.com/c/en/us/support/collaboration-endpoints/spark-room-kit-series/series.html#~tab-documents");
    private static readonly StringComparer StringComparer                = StringComparer.Create(CultureInfo.GetCultureInfo("en-US"), true);

    private readonly IBrowsingContext _anglesharp = BrowsingContext.New(Configuration.Default.With(new DefaultHttpRequester(UserAgentHeaderValue)).WithDefaultLoader());
    private readonly IGitHubClient    _gitHubClient;
    private readonly bool             _isDryRun;

    public static async Task<int> Main(string[] args) {
        CommandLineApplication argumentParser = new();
        CommandOption<string> gitHubAccessTokenOption =
            argumentParser.Option<string>("--github-access-token", $"Token with repo scope access to {RepositoryOwner}/{RepositoryName}", CommandOptionType.SingleValue);
        CommandOption<bool> dryRunOption = argumentParser.Option<bool>("-n|--dry-run", "Don't actually file any issues", CommandOptionType.NoValue);
        argumentParser.Parse(args);
        if (gitHubAccessTokenOption.Value() is not { } gitHubAccessToken) {
            PrintUsage();
            return 1;
        }

        await new IssueCreator(gitHubAccessToken, dryRunOption.ParsedValue).CreateIssueIfMissing();
        return 0;
    }

    private static void PrintUsage() => Console.WriteLine($"Usage: {Path.GetFileName(Environment.ProcessPath)} --github-access-token ghp_XXXXXXXXX [--dry-run]");

    private IssueCreator(string gitHubAccessToken, bool isDryRun) {
        _isDryRun     = isDryRun;
        _gitHubClient = new GitHubClient(new ProductHeaderValue(CurrentAssembly.Name!, CurrentAssembly.Version!.ToString(3))) { Credentials = new Credentials(gitHubAccessToken) };
    }

    private async Task CreateIssueIfMissing() {
        Console.WriteLine("Checking version of latest documented release...");
        PublishedDocumentation latestDocumentation = await findLatestDocumentation();
        Console.WriteLine($"Latest documentation version: {latestDocumentation.Name}");

        Console.WriteLine("Checking existing GitHub issues...");
        if (await findIssueForLatestDocumentation(latestDocumentation) is not { } existingIssue) {
            Console.WriteLine("No existing issues found, so creating a new issue...");
            NewIssue issue = ConstructIssue(latestDocumentation);
            if (!_isDryRun) {
                Issue publishedIssue = await PublishIssue(issue);
                Console.WriteLine($"Created issue #{publishedIssue.Number} at {publishedIssue.HtmlUrl}");
            } else {
                Console.WriteLine("Would have created a new issue if not in dry-run mode.");
            }
            Console.WriteLine(
                $"""
                 Repository:  {RepositoryOwner}/{RepositoryName}
                 Title:       {issue.Title}
                 Assigned to: {issue.Assignees.JoinHumanized()}
                 Labels:      {issue.Labels.Select(label => $"\"{label}\"").Join(separator: ", ")}
                 Body:        {issue.Body}
                 """);
        } else {
            Console.WriteLine($"GitHub issue #{existingIssue.Number} already exists, so not creating a new issue");
        }
    }

    private async Task<PublishedDocumentation> findLatestDocumentation() {
        using IDocument listPage = await _anglesharp.OpenAsync(DocumentationListPageLocation);

        if (listPage.StatusCode >= HttpStatusCode.BadRequest) {
            throw new ApplicationException($"Fetching documentation list page failed with status code {listPage.StatusCode}");
        }

        IHtmlAnchorElement latestDocumentationLink = listPage.QuerySelector<IHtmlAnchorElement>("#Reference + h3 + ul li a")!;

        string releaseName = ReleaseNamePattern().Match(latestDocumentationLink.Text).Groups["name"].Value;

        return new PublishedDocumentation(releaseName, new Uri(latestDocumentationLink.Href));
    }

    private async Task<Issue?> findIssueForLatestDocumentation(PublishedDocumentation documentation) {
        IReadOnlyList<Issue> upstreamUpdateIssues = await _gitHubClient.Issue.GetAllForRepository(RepositoryOwner, RepositoryName, new RepositoryIssueRequest {
            Labels = { IssueLabel },
            State  = ItemStateFilter.All
        });
        string expectedIssueTitle = GetIssueTitle(documentation.Name);
        return upstreamUpdateIssues.FirstOrDefault(issue => StringComparer.Equals(issue.Title, expectedIssueTitle));
    }

    private async Task<Issue> PublishIssue(NewIssue issue) => await _gitHubClient.Issue.Create(RepositoryOwner, RepositoryName, issue);

    private static NewIssue ConstructIssue(PublishedDocumentation documentation) => new(GetIssueTitle(documentation.Name)) {
        Assignees = { RepositoryOwner },
        Labels    = { IssueLabel },
        // ⚠ Warning: The first line of the Body string must remain unchanged because an email filter matches it in my MDaemon user account
        Body = $"""
                Cisco has released documentation for a new on-premises endpoint software release.

                - **[{documentation.Name} API documentation PDF]({documentation.Location})**
                    - [All PDF versions]({DocumentationListPageLocation})
                    - [All PDF versions (alternative list)](https://www.cisco.com/c/en/us/support/collaboration-endpoints/spark-room-kit-series/products-command-reference-list.html)
                    - [API documentation website](https://roomos.cisco.com/xapi)
                - [Release notes](https://roomos.cisco.com/print/WhatsNew/ReleaseNotesRoomOS_11)
                - [Software downloads](https://software.cisco.com/download/home/286314238/type/280886992/release/)
                """
    };

    private static string GetIssueTitle(string releaseName) => $"Update for {releaseName}";

    [GeneratedRegex(@"\((?<name>.*?)\)")]
    private static partial Regex ReleaseNamePattern();

}