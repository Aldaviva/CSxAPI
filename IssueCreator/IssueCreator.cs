using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Io;
using McMaster.Extensions.CommandLineUtils;
using Octokit;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Unfucked;

namespace IssueCreator;

internal sealed partial class IssueCreator: IDisposable {

    private const string RepositoryOwner      = "Aldaviva";
    private const string RepositoryName       = "CSxAPI";
    private const string IssueLabel           = "upstream update";
    private const string UserAgentHeaderValue = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0.0.0 Safari/537.36";

    private static readonly Assembly       CurrentAssembly = Assembly.GetExecutingAssembly();
    private static readonly StringComparer StringComparer  = StringComparer.Create(CultureInfo.GetCultureInfo("en-US"), true);

    private static readonly Url DocumentationListPageLocation = new(
        "https://www.cisco.com/c/en/us/support/collaboration-endpoints/spark-room-kit-series/series.html#~tab-documents:~:text=Reference-,Command%20References,-Applicable%20to%20Multiple");

    private readonly IBrowsingContext _anglesharp = BrowsingContext.New(Configuration.Default.With(new DefaultHttpRequester(UserAgentHeaderValue,
        static request => {
            request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7";
            request.Headers.Add("sec-ch-ua", "\"Not;A=Brand\";v=\"8\", \"Chromium\";v=\"150\", \"Google Chrome\";v=\"150\"");
            request.Headers.Add("sec-ch-ua-mobile", "?0");
            request.Headers.Add("sec-ch-ua-platform", "Windows");
            request.Headers.Add("Sec-Fetch-Site", "none");
        })).WithDefaultLoader());

    private readonly IGitHubClient           _gitHubClient;
    private readonly bool                    _isDryRun;
    private readonly CancellationTokenSource _cts = new CancellationTokenSource().CancelOnCtrlC();

    public static async Task<int> Main(string[] args) {
        using CommandLineApplication argumentParser = new();
        argumentParser.Conventions.UseDefaultConventions();
        argumentParser.VersionOptionFromAssemblyAttributes(CurrentAssembly);
        CommandOption<string> gitHubAccessTokenOption = argumentParser.Option<string>("--github-access-token",
            $"Token with repo scope access to {RepositoryOwner}/{RepositoryName}", CommandOptionType.SingleValue).IsRequired();
        CommandOption<bool> dryRunOption = argumentParser.Option<bool>("-n|--dry-run", "Don't actually file any issues", CommandOptionType.NoValue);
        argumentParser.Parse(args);
        if (argumentParser.IsShowingInformation) {
            return 0;
        } else if (argumentParser.GetValidationResult()?.ErrorMessage is {} validationError) {
            Console.WriteLine(validationError);
            return 1;
        }

        using IssueCreator issueCreator = new(gitHubAccessTokenOption.ParsedValue, dryRunOption.ParsedValue);
        try {
            await issueCreator.CreateIssueIfMissing();
        } catch (OperationCanceledException) {
            return 1;
        }
        return 0;
    }

    private IssueCreator(string gitHubAccessToken, bool isDryRun) {
        _isDryRun     = isDryRun;
        _gitHubClient = new GitHubClient(new ProductHeaderValue(CurrentAssembly.GetName().Name!, CurrentAssembly.GetName().Version!.ToString(3))) { Credentials = new Credentials(gitHubAccessToken) };
    }

    private async Task CreateIssueIfMissing() {
        Console.WriteLine("Checking version of latest documented release...");
        PublishedDocumentation latestDocumentation = await findLatestDocumentation();
        Console.WriteLine($"Latest documentation version: {latestDocumentation.Name}");

        Console.WriteLine("Checking existing GitHub issues...");
        if (await findIssueForLatestDocumentation(latestDocumentation) is not {} existingIssue) {
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
        using IDocument listPage = await _anglesharp.OpenAsync(DocumentationListPageLocation, _cts.Token);

        if (listPage.StatusCode >= HttpStatusCode.BadRequest) {
            throw new ApplicationException($"Fetching documentation list page {DocumentationListPageLocation} failed with status code {listPage.StatusCode}");
        }

        IHtmlAnchorElement latestDocumentationLink = listPage.QuerySelector<IHtmlAnchorElement>("#Reference + h3 + ul li a")!;

        string releaseName = ReleaseNamePattern().Match(latestDocumentationLink.Text).Groups["name"].Value;

        return new PublishedDocumentation(releaseName, new Uri(latestDocumentationLink.Href));
    }

    private async Task<Issue?> findIssueForLatestDocumentation(PublishedDocumentation documentation) {
        _cts.Token.ThrowIfCancellationRequested();

        RepositoryIssueRequest criteria = new() { Labels = { IssueLabel }, State = ItemStateFilter.All };
        IAsyncEnumerable<Issue> upstreamUpdateIssues = ForAsync<int, Issue>(Enumerable.InfiniteSequence(1, 1), async (page, _) => await _gitHubClient.Issue.GetAllForRepository(
            RepositoryOwner, RepositoryName, criteria, new ApiOptions { PageSize = 100, StartPage = page }), _cts.Token);

        string expectedIssueTitle = GetIssueTitle(documentation.Name);
        return await upstreamUpdateIssues.FirstOrDefaultAsync(issue => StringComparer.Equals(issue.Title, expectedIssueTitle));
    }

    private async Task<Issue> PublishIssue(NewIssue issue) {
        _cts.Token.ThrowIfCancellationRequested();
        return await _gitHubClient.Issue.Create(RepositoryOwner, RepositoryName, issue);
    }

    private static NewIssue ConstructIssue(PublishedDocumentation documentation) => new(GetIssueTitle(documentation.Name)) {
        Assignees = { RepositoryOwner },
        Labels    = { IssueLabel },
        // ⚠ Warning: The first line of the Body string must remain unchanged because an email filter matches it in my MDaemon user account
        Body = $"""
            Cisco has released documentation for a new on-premises endpoint software release.

            - **[{documentation.Name} API documentation PDF]({documentation.Location})**
                - [All PDF versions]({DocumentationListPageLocation}) ([alternative](https://www.cisco.com/c/en/us/support/collaboration-endpoints/spark-room-kit-series/products-command-reference-list.html))
                - [API documentation website](https://roomos.cisco.com/xapi)
            - [Release notes](https://roomos.cisco.com/print/WhatsNew/ReleaseNotesRoomOS_11)
            - [Software downloads](https://software.cisco.com/download/home/286314238/type/280886992/release/)
            """
    };

    private static string GetIssueTitle(string releaseName) => $"Update for {releaseName}";

    private static async IAsyncEnumerable<T> ForAsync<K, T>(IEnumerable<K> keys, Func<K, CancellationToken, Task<IEnumerable<T>>> callback,
                                                            [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        foreach (K key in keys) {
            IEnumerable<T> page      = await callback(key, cancellationToken);
            bool           emptyPage = true;
            foreach (T item in page) {
                emptyPage = false;
                yield return item;
            }
            if (emptyPage) {
                yield break;
            }
        }
    }

    [GeneratedRegex(@"\((?<name>.*?)\)")]
    private static partial Regex ReleaseNamePattern();

    public void Dispose() {
        _anglesharp.Dispose();
    }

}