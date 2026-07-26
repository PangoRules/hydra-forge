using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Screens;
using HydraForge.Tui.Services;
using Xunit;

namespace HydraForge.Tui.Tests.UnitTests;

/// <summary>
/// Extended TestApiClient that also mocks ProjectsGETAsync for ProjectListScreen tests.
/// </summary>
public class ProjectListTestApiClient : TestApiClient
{
    public bool ThrowOnProjects { get; set; } = false;
    public bool ThrowApiExceptionOnProjects { get; set; } = false;
    public int TotalCount { get; set; } = 2;
    public int CallCount { get; private set; }

    public bool? LastIncludeArchived { get; private set; }
    public string? LastSearch { get; private set; }
    public ProjectSortField? LastSortBy { get; private set; }
    public bool? LastSortDescending { get; private set; }
    public MemberRole? LastRole { get; private set; }
    public int? LastSkip { get; private set; }
    public int? LastTake { get; private set; }

    public override Task<ProjectListPageResponse> ProjectsGETAsync(
        bool? includeArchived = null,
        string? search = null,
        ProjectSortField? sortBy = null,
        bool? sortDescending = null,
        MemberRole? role = null,
        int? skip = null,
        int? take = null,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastIncludeArchived = includeArchived;
        LastSearch = search;
        LastSortBy = sortBy;
        LastSortDescending = sortDescending;
        LastRole = role;
        LastSkip = skip;
        LastTake = take;

        if (ThrowApiExceptionOnProjects)
            throw new ApiException("deserialization failed", 200, null, new Dictionary<string, IEnumerable<string>>(), null);

        if (ThrowOnProjects)
            throw new HttpRequestException("Network error");

        return Task.FromResult(new ProjectListPageResponse
        {
            Items = new System.Collections.ObjectModel.ObservableCollection<ProjectListResponse>
            {
                new ProjectListResponse
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Project",
                    CreatedAt = DateTimeOffset.UtcNow,
                    MemberCount = 1,
                    MyRole = MemberRole.Owner
                },
                new ProjectListResponse
                {
                    Id = Guid.NewGuid(),
                    Name = "Second Project",
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                    MemberCount = 2,
                    MyRole = MemberRole.Member
                }
            },
            TotalCount = TotalCount
        });
    }

    public override Task<ProjectResponse> ProjectsPOSTAsync(CreateProjectRequest request, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ProjectResponse
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = "",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }
}

public class ProjectListScreenTests
{
    [Fact]
    public async Task ProjectListScreen_Should_Render_Without_Throwing()
    {
        // Arrange
        var testConfig = new TuiConfig { ServerUrl = "http://localhost:5000" };
        var configStore = new TestConfigStore { Config = testConfig };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        var mockApiClient = new ProjectListTestApiClient();
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);

        var signalRConnectionManager = new SignalRConnectionManager(appState, errorCollector);
        var notificationCenter = new NotificationCenter(apiClientFactory, appState, errorCollector);
        var projectListScreen = new ProjectListScreen(apiClientFactory, appState, errorCollector, new ConnectionManager(appState, apiClientFactory, errorCollector), signalRConnectionManager, notificationCenter);

        // Act & Assert - RenderAsync should not throw
        await projectListScreen.RenderAsync();
    }

    [Fact]
    public async Task ProjectListScreen_HandleKeyAsync_Should_Change_Selected_Index_With_J_K()
    {
        // Arrange
        var testConfig = new TuiConfig { ServerUrl = "http://localhost:5000" };
        var configStore = new TestConfigStore { Config = testConfig };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        var mockApiClient = new ProjectListTestApiClient();
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);

        var signalRConnectionManager = new SignalRConnectionManager(appState, errorCollector);
        var notificationCenter = new NotificationCenter(apiClientFactory, appState, errorCollector);
        var projectListScreen = new ProjectListScreen(apiClientFactory, appState, errorCollector, new ConnectionManager(appState, apiClientFactory, errorCollector), signalRConnectionManager, notificationCenter);
        
        // Load some test projects
        await projectListScreen.OnEnterAsync();

        // Act - Handle J key (down)
        await projectListScreen.HandleKeyAsync(new ConsoleKeyInfo('j', ConsoleKey.J, false, false, false));

        // Assert
        Assert.Equal(1, projectListScreen.GetType()
            .GetField("_selectedIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(projectListScreen));

        // Act - Handle K key (up)
        await projectListScreen.HandleKeyAsync(new ConsoleKeyInfo('k', ConsoleKey.K, false, false, false));

        // Assert
        Assert.Equal(0, projectListScreen.GetType()
            .GetField("_selectedIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(projectListScreen));
    }

    [Fact]
    public async Task ProjectListScreen_HandleKeyAsync_Should_Toggle_Archived()
    {
        // Arrange
        var testConfig = new TuiConfig { ServerUrl = "http://localhost:5000" };
        var configStore = new TestConfigStore { Config = testConfig };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        var mockApiClient = new ProjectListTestApiClient();
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);

        var signalRConnectionManager = new SignalRConnectionManager(appState, errorCollector);
        var notificationCenter = new NotificationCenter(apiClientFactory, appState, errorCollector);
        var projectListScreen = new ProjectListScreen(apiClientFactory, appState, errorCollector, new ConnectionManager(appState, apiClientFactory, errorCollector), signalRConnectionManager, notificationCenter);
        
        // Load some test projects
        await projectListScreen.OnEnterAsync();

        // Act - Handle A key
        await projectListScreen.HandleKeyAsync(new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false));

        // Assert - _showArchived should be true
        var showArchived = projectListScreen.GetType()
            .GetField("_showArchived", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(projectListScreen);
            
        Assert.True((bool)showArchived!);
    }

    // Note: Q and C key tests are skipped because Spectre.Console prompts
    // (AnsiConsole.Confirm, AnsiConsole.Prompt) throw in test environment.
    // These should be tested via integration/E2E tests instead.

    [Fact]
    public async Task LoadProjectsAsync_OnApiException_RecordsErrorInsteadOfSwallowingIt()
    {
        var testConfig = new TuiConfig { ServerUrl = "http://localhost:5000" };
        var configStore = new TestConfigStore { Config = testConfig };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        var mockApiClient = new ProjectListTestApiClient { ThrowApiExceptionOnProjects = true };
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);
        var signalRConnectionManager = new SignalRConnectionManager(appState, errorCollector);
        var notificationCenter = new NotificationCenter(apiClientFactory, appState, errorCollector);
        var projectListScreen = new ProjectListScreen(apiClientFactory, appState, errorCollector, new ConnectionManager(appState, apiClientFactory, errorCollector), signalRConnectionManager, notificationCenter);

        await projectListScreen.OnEnterAsync();

        // OnEnterAsync also attempts to connect NotificationHub, which fails against the
        // fake server URL and adds its own (expected, non-fatal) error — assert on the
        // project-load failure specifically rather than an exact total count.
        Assert.Contains(errorCollector.GetErrors(), e => e.Message.Contains("Failed to load projects"));
    }

    [Fact]
    public async Task RenderAsync_WithErrorsPresent_DoesNotThrow()
    {
        var testConfig = new TuiConfig { ServerUrl = "http://localhost:5000" };
        var configStore = new TestConfigStore { Config = testConfig };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        errorCollector.Add("corr-1", "Simulated failure");
        var mockApiClient = new ProjectListTestApiClient();
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);
        var signalRConnectionManager = new SignalRConnectionManager(appState, errorCollector);
        var notificationCenter = new NotificationCenter(apiClientFactory, appState, errorCollector);
        var projectListScreen = new ProjectListScreen(apiClientFactory, appState, errorCollector, new ConnectionManager(appState, apiClientFactory, errorCollector), signalRConnectionManager, notificationCenter);

        var exception = await Record.ExceptionAsync(projectListScreen.RenderAsync);

        Assert.Null(exception);
    }

    [Fact]
    public async Task HandleKeyAsync_WithXKey_DismissesAllErrors()
    {
        var testConfig = new TuiConfig { ServerUrl = "http://localhost:5000" };
        var configStore = new TestConfigStore { Config = testConfig };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        errorCollector.Add("corr-1", "First failure");
        errorCollector.Add("corr-2", "Second failure");
        var mockApiClient = new ProjectListTestApiClient();
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);
        var signalRConnectionManager = new SignalRConnectionManager(appState, errorCollector);
        var notificationCenter = new NotificationCenter(apiClientFactory, appState, errorCollector);
        var projectListScreen = new ProjectListScreen(apiClientFactory, appState, errorCollector, new ConnectionManager(appState, apiClientFactory, errorCollector), signalRConnectionManager, notificationCenter);
        await projectListScreen.OnEnterAsync();

        await projectListScreen.HandleKeyAsync(new ConsoleKeyInfo('x', ConsoleKey.X, false, false, false));

        Assert.Equal(0, errorCollector.Count);
    }

    [Fact]
    public async Task HandleKeyAsync_WithNKey_AdvancesPageWhenMoreResultsExist()
    {
        var mockApiClient = new ProjectListTestApiClient { TotalCount = 45 };
        var (screen, _) = CreateScreen(mockApiClient);
        await screen.OnEnterAsync();

        await screen.HandleKeyAsync(new ConsoleKeyInfo('n', ConsoleKey.N, false, false, false));

        Assert.Equal(20, mockApiClient.LastSkip);
    }

    [Fact]
    public async Task HandleKeyAsync_WithNKey_NoOpAtLastPage()
    {
        var mockApiClient = new ProjectListTestApiClient { TotalCount = 2 };
        var (screen, _) = CreateScreen(mockApiClient);
        await screen.OnEnterAsync();
        var callsBefore = mockApiClient.CallCount;

        await screen.HandleKeyAsync(new ConsoleKeyInfo('n', ConsoleKey.N, false, false, false));

        Assert.Equal(callsBefore, mockApiClient.CallCount);
    }

    [Fact]
    public async Task HandleKeyAsync_WithPKey_NoOpAtFirstPage()
    {
        var mockApiClient = new ProjectListTestApiClient { TotalCount = 45 };
        var (screen, _) = CreateScreen(mockApiClient);
        await screen.OnEnterAsync();
        var callsBefore = mockApiClient.CallCount;

        await screen.HandleKeyAsync(new ConsoleKeyInfo('p', ConsoleKey.P, false, false, false));

        Assert.Equal(callsBefore, mockApiClient.CallCount);
        Assert.Equal(0, mockApiClient.LastSkip);
    }

    [Fact]
    public async Task HandleKeyAsync_WithPKey_GoesBackAPageAfterAdvancing()
    {
        var mockApiClient = new ProjectListTestApiClient { TotalCount = 45 };
        var (screen, _) = CreateScreen(mockApiClient);
        await screen.OnEnterAsync();
        await screen.HandleKeyAsync(new ConsoleKeyInfo('n', ConsoleKey.N, false, false, false));

        await screen.HandleKeyAsync(new ConsoleKeyInfo('p', ConsoleKey.P, false, false, false));

        Assert.Equal(0, mockApiClient.LastSkip);
    }

    [Fact]
    public async Task HandleKeyAsync_WithRKey_CyclesRoleFilterThroughApiCalls()
    {
        var mockApiClient = new ProjectListTestApiClient();
        var (screen, _) = CreateScreen(mockApiClient);
        await screen.OnEnterAsync();
        var rKey = new ConsoleKeyInfo('r', ConsoleKey.R, false, false, false);

        await screen.HandleKeyAsync(rKey);
        Assert.Equal(MemberRole.Owner, mockApiClient.LastRole);

        await screen.HandleKeyAsync(rKey);
        Assert.Equal(MemberRole.Member, mockApiClient.LastRole);

        await screen.HandleKeyAsync(rKey);
        Assert.Null(mockApiClient.LastRole);
    }

    [Fact]
    public async Task HandleKeyAsync_WithSKey_CyclesSortField()
    {
        var mockApiClient = new ProjectListTestApiClient();
        var (screen, _) = CreateScreen(mockApiClient);
        await screen.OnEnterAsync();
        Assert.Equal(ProjectSortField.CreatedAt, mockApiClient.LastSortBy);
        var sKey = new ConsoleKeyInfo('s', ConsoleKey.S, false, false, false);

        await screen.HandleKeyAsync(sKey);
        Assert.Equal(ProjectSortField.UpdatedAt, mockApiClient.LastSortBy);

        await screen.HandleKeyAsync(sKey);
        Assert.Equal(ProjectSortField.Name, mockApiClient.LastSortBy);

        await screen.HandleKeyAsync(sKey);
        Assert.Equal(ProjectSortField.CreatedAt, mockApiClient.LastSortBy);
    }

    [Fact]
    public async Task HandleKeyAsync_WithShiftSKey_TogglesSortDirection()
    {
        var mockApiClient = new ProjectListTestApiClient();
        var (screen, _) = CreateScreen(mockApiClient);
        await screen.OnEnterAsync();
        Assert.True(mockApiClient.LastSortDescending);
        var shiftS = new ConsoleKeyInfo('S', ConsoleKey.S, shift: true, false, false);

        await screen.HandleKeyAsync(shiftS);

        Assert.False(mockApiClient.LastSortDescending);
    }

    [Fact]
    public async Task HandleKeyAsync_ChangingFilters_ResetsToFirstPage()
    {
        var mockApiClient = new ProjectListTestApiClient { TotalCount = 45 };
        var (screen, _) = CreateScreen(mockApiClient);
        await screen.OnEnterAsync();
        await screen.HandleKeyAsync(new ConsoleKeyInfo('n', ConsoleKey.N, false, false, false));
        Assert.Equal(20, mockApiClient.LastSkip);

        await screen.HandleKeyAsync(new ConsoleKeyInfo('a', ConsoleKey.A, false, false, false));

        Assert.Equal(0, mockApiClient.LastSkip);
    }

    private static (ProjectListScreen Screen, ErrorCollector ErrorCollector) CreateScreen(ProjectListTestApiClient mockApiClient)
    {
        var configStore = new TestConfigStore { Config = new TuiConfig { ServerUrl = "http://localhost:5000" } };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);
        var signalRConnectionManager = new SignalRConnectionManager(appState, errorCollector);
        var notificationCenter = new NotificationCenter(apiClientFactory, appState, errorCollector);
        var screen = new ProjectListScreen(apiClientFactory, appState, errorCollector, new ConnectionManager(appState, apiClientFactory, errorCollector), signalRConnectionManager, notificationCenter);
        return (screen, errorCollector);
    }
}
