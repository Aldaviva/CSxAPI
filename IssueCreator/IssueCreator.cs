using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using McMaster.Extensions.CommandLineUtils;
using Octokit;
using System.Reflection;
using System.Text.RegularExpressions;

namespace IssueCreator;

internal class IssueCreator {

    private const string RepositoryOwner = "Aldaviva";
    private const string RepositoryName  = "CSxAPI";
    private const string IssueLabel      = "upstream update";

    private static readonly AssemblyName Assembly = System.Reflection.Assembly.GetExecutingAssembly().GetName();
    private static readonly Url DocumentationListPageLocation = new("https://www.cisco.com/c/en/us/support/collaboration-endpoints/spark-room-kit-series/products-command-reference-list.html");

    private readonly IBrowsingContext _anglesharp = BrowsingContext.New(Configuration.Default.WithDefaultLoader());
    private readonly IGitHubClient    _gitHubClient;
    private readonly bool             _isDryRun;

    public static async Task<int> Main(string[] args) {
        CommandLineApplication argumentParser = new();
        CommandOption<string> gitHubAccessTokenOption =
            argumentParser.Option<string>("--github-access-token", $"Token with repo scope access to {RepositoryOwner}/{RepositoryName}", CommandOptionType.SingleValue);
        CommandOption<bool> dryRunOption = argumentParser.Option<bool>("-n|--dry-run", "Don't actually file any issues", CommandOptionType.NoValue);
        argumentParser.Parse(args);
        if (gitHubAccessTokenOption.Value() is not { } gitHubAccessToken) {
            Console.WriteLine($"Usage: {Path.GetFileName(Environment.ProcessPath)} --{gitHubAccessTokenOption.LongName} ghp_XXXXXXXXX [-{dryRunOption.ShortName}|--{dryRunOption.LongName}]");
            return 1;
        }

        await new IssueCreator(gitHubAccessToken, dryRunOption.ParsedValue).CreateIssueIfMissing();
        return 0;
    }

    private IssueCreator(string gitHubAccessToken, bool isDryRun) {
        _isDryRun     = isDryRun;
        _gitHubClient = new GitHubClient(new ProductHeaderValue(Assembly.Name!, Assembly.Version!.ToString(3))) { Credentials = new Credentials(gitHubAccessToken) };
    }

    private async Task CreateIssueIfMissing() {
        Console.WriteLine("Checking version of latest documented release...");
        PublishedDocumentation latestDocumentation = await findLatestDocumentation();
        Console.WriteLine($"Latest documentation version: {latestDocumentation.Name}");

        if (await findIssueForLatestDocumentation(latestDocumentation) is not { } existingIssue) {
            Console.WriteLine("No existing issues found, so creating a new issue...");
            if (!_isDryRun) {
                Issue newIssue = await CreateIssue(latestDocumentation);
                Console.WriteLine($"Created issue #{newIssue.Number}: {newIssue.Title}");
            } else {
                Console.WriteLine("Would have created a new issue if not in dry-run mode.");
            }
        } else {
            Console.WriteLine($"GitHub issue #{existingIssue.Number} already exists, so not creating a new issue");
        }
    }

    private async Task<PublishedDocumentation> findLatestDocumentation() {
        using IDocument listPage = await _anglesharp.OpenAsync(DocumentationListPageLocation);

        IHtmlAnchorElement latestDocumentationLink = listPage.QuerySelectorAll(".heading")
            .First(element => element.Text().StartsWith("Cisco "))
            .NextElementSibling!
            .QuerySelector<IHtmlAnchorElement>("a")!;

        string releaseName = Regex.Match(latestDocumentationLink.Text, @"\((?<name>.*?)\)").Groups["name"].Value;

        return new PublishedDocumentation(releaseName, new Uri(latestDocumentationLink.Href));
    }

    private async Task<Issue?> findIssueForLatestDocumentation(PublishedDocumentation documentation) {
        IReadOnlyList<Issue> upstreamUpdateIssues = await _gitHubClient.Issue.GetAllForRepository(RepositoryOwner, RepositoryName, new RepositoryIssueRequest {
            Labels = { IssueLabel },
            State  = ItemStateFilter.All
        });
        string expectedIssueTitle = GetIssueTitle(documentation.Name);
        return upstreamUpdateIssues.FirstOrDefault(issue => issue.Title == expectedIssueTitle);
    }

    private async Task<Issue> CreateIssue(PublishedDocumentation documentation) => await _gitHubClient.Issue.Create(RepositoryOwner, RepositoryName,
        new NewIssue(GetIssueTitle(documentation.Name)) {
            Assignees = { RepositoryOwner },
            Labels    = { IssueLabel },
            // ⚠ Warning: The first line of the Body string must remain unchanged because an email filter matches it
            Body = $"""
                    Cisco has released documentation for a new on-premises endpoint software release.

                    - **[{documentation.Name} API documentation PDF]({documentation.Location})**
                        - [all PDF versions]({DocumentationListPageLocation})
                        - [API documentation website](https://roomos.cisco.com/xapi)
                    - [Release notes](https://roomos.cisco.com/print/WhatsNew/ReleaseNotesRoomOS_11)
                    - [Software downloads](https://software.cisco.com/download/home/286314238/type/280886992/release/)
                    """
        });

    private static string GetIssueTitle(string releaseName) => $"Update for {releaseName}";

}