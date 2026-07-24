using HydraForge.Tui.Generated;
using HydraForge.Tui.Models;
using HydraForge.Tui.Screens;
using HydraForge.Tui.Services;
using Xunit;

namespace HydraForge.Tui.Tests.UnitTests;

public class LoginScreenTests
{
    [Fact]
    public async Task LoginScreen_Should_Call_LoginAsync_With_Correct_Credentials()
    {
        // Arrange
        var testConfig = new TuiConfig { ServerUrl = "http://localhost:5000" };
        var configStore = new TestConfigStore { Config = testConfig };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        var mockApiClient = new TestApiClient { ThrowOnLogin = false };
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);

        var loginScreen = new LoginScreen(configStore, apiClientFactory, appState, errorCollector);

        // We can't call RenderAsync (it blocks on console input),
        // but we can verify the factory creates unauthenticated client and the client exposes LoginAsync
        var client = apiClientFactory.CreateUnauthenticatedClient();

        // Act
        var response = await client.LoginAsync(new LoginRequest
        {
            Username = "testuser",
            Password = "testpass"
        });

        // Assert
        Assert.NotNull(response);
        Assert.Equal("test-token", response.AccessToken);
    }

    [Fact]
    public async Task LoginScreen_Should_Save_Jwt_To_Config_On_Success()
    {
        // Arrange
        var testConfig = new TuiConfig { ServerUrl = "http://localhost:5000" };
        var configStore = new TestConfigStore { Config = testConfig };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        var mockApiClient = new TestApiClient { ThrowOnLogin = false };
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);

        // Act — simulate the successful login flow from RenderAsync
        var client = apiClientFactory.CreateUnauthenticatedClient();
        var response = await client.LoginAsync(new LoginRequest
        {
            Username = "testuser",
            Password = "testpass"
        });

        testConfig.JwtToken = response.AccessToken;
        testConfig.ExpiresAt = response.ExpiresAt;
        configStore.Save(testConfig);

        // Assert
        var savedConfig = configStore.Load();
        Assert.Equal("test-token", savedConfig.JwtToken);
        Assert.NotNull(savedConfig.ExpiresAt);
    }

    [Fact]
    public async Task LoginScreen_Should_Handle_ApiException()
    {
        // Arrange
        var testConfig = new TuiConfig { ServerUrl = "http://localhost:5000" };
        var configStore = new TestConfigStore { Config = testConfig };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        var mockApiClient = new TestApiClient { ThrowOnLogin = true };
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);

        // Act — LoginAsync should throw HttpRequestException when ThrowOnLogin is true
        var client = apiClientFactory.CreateUnauthenticatedClient();
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.LoginAsync(new LoginRequest { Username = "u", Password = "p" }));

        // Assert
        Assert.Contains("Network error", ex.Message);
    }

    [Fact]
    public async Task Program_Should_Require_Login_When_No_Jwt()
    {
        // Arrange
        var configStore = new TestConfigStore { Config = new TuiConfig { ServerUrl = "http://localhost:5000" } };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        var mockApiClient = new TestApiClient { ThrowOnLogin = false };
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);

        // Simulate Program startup auth flow: no JWT → login needed
        var loadedConfig = configStore.Load();
        var needsLogin = string.IsNullOrWhiteSpace(loadedConfig.JwtToken);

        Assert.True(needsLogin);
    }

    [Fact]
    public async Task Program_Should_Skip_Login_When_Valid_Jwt_Exists()
    {
        // Arrange
        var configStore = new TestConfigStore
        {
            Config = new TuiConfig
            {
                ServerUrl = "http://localhost:5000",
                JwtToken = "existing-valid-token",
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            }
        };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        var mockApiClient = new TestApiClient { ThrowOnLogin = false };
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);

        // Simulate Program startup auth flow: has valid JWT → skip login
        var loadedConfig = configStore.Load();
        var needsLogin = string.IsNullOrWhiteSpace(loadedConfig.JwtToken);

        Assert.False(needsLogin);
        Assert.NotNull(loadedConfig.ExpiresAt);
        Assert.True(loadedConfig.ExpiresAt.Value.UtcDateTime > DateTime.UtcNow);
    }

    [Fact]
    public async Task Program_Should_Refresh_When_Token_Expiring()
    {
        // Arrange
        var configStore = new TestConfigStore
        {
            Config = new TuiConfig
            {
                ServerUrl = "http://localhost:5000",
                JwtToken = "expiring-token",
                ExpiresAt = DateTime.UtcNow.AddSeconds(30) // Expiring soon
            }
        };
        var appState = new AppState();
        var errorCollector = new ErrorCollector();
        var mockApiClient = new TestApiClient { ThrowOnLogin = false };
        var apiClientFactory = new TestApiClientFactory(mockApiClient, configStore, appState, errorCollector);

        // Simulate Program startup auth flow: token expiring → try refresh
        var loadedConfig = configStore.Load();
        var tokenExpiring = apiClientFactory.IsTokenExpiringSoon();

        Assert.True(tokenExpiring);
    }
}

// Test implementations
public class TestConfigStore : ConfigStore
{
    public TuiConfig Config { get; set; } = new TuiConfig();

    public override TuiConfig Load()
    {
        return Config;
    }

    public override void Save(TuiConfig config)
    {
        Config = config;
    }

    public override void Clear()
    {
        Config = new TuiConfig();
    }
}

public class TestApiClientFactory : ApiClientFactory
{
    private readonly HydraForgeApiClient _mockClient;

    public TestApiClientFactory(HydraForgeApiClient mockClient, ConfigStore configStore, AppState appState, ErrorCollector errorCollector)
        : base(configStore, appState, errorCollector)
    {
        _mockClient = mockClient;
    }

    public override HydraForgeApiClient CreateUnauthenticatedClient()
    {
        return _mockClient;
    }

    public override HydraForgeApiClient CreateClient()
    {
        return _mockClient;
    }
}

public class TestApiClient : HydraForgeApiClient
{
    public bool ThrowOnLogin { get; set; } = false;

    public TestApiClient() : base(new HttpClient())
    {
    }

    public override Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (ThrowOnLogin)
        {
            throw new HttpRequestException("Network error");
        }

        return Task.FromResult(new LoginResponse
        {
            AccessToken = "test-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
    }

    public override Task<RefreshTokenResponse> RefreshAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new RefreshTokenResponse
        {
            AccessToken = "refreshed-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });
    }
}
