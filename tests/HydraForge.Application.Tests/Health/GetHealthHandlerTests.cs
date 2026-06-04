using HydraForge.Application.Health;

namespace HydraForge.Application.Tests.Health;

public class GetHealthHandlerTests
{
    [Fact]
    public async Task Handle_AllHealthy_ReturnsHealthy()
    {
        var serverProbe = new FakeServerProbe(HealthStatus.Healthy);
        var dbProbe = new FakeDbProbe(HealthStatus.Healthy);
        var llmProbe = new FakeLlmProbe(HealthStatus.NotConfigured);

        var handler = new GetHealthHandler(new IHealthProbe[] { serverProbe, dbProbe, llmProbe });
        var result = await handler.HandleAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(HealthStatus.Healthy, result.Value.OverallStatus);
        Assert.Equal(HealthStatus.Healthy, result.Value.ServerStatus);
        Assert.Equal(HealthStatus.Healthy, result.Value.DatabaseStatus);
        Assert.Equal(HealthStatus.NotConfigured, result.Value.LlmStatus);
    }

    [Fact]
    public async Task Handle_DbUnhealthy_ReturnsUnhealthy()
    {
        var serverProbe = new FakeServerProbe(HealthStatus.Healthy);
        var dbProbe = new FakeDbProbe(HealthStatus.Unhealthy);
        var llmProbe = new FakeLlmProbe(HealthStatus.Healthy);

        var handler = new GetHealthHandler(new IHealthProbe[] { serverProbe, dbProbe, llmProbe });
        var result = await handler.HandleAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(HealthStatus.Unhealthy, result.Value.OverallStatus);
    }

    [Fact]
    public async Task Handle_LlmNotConfigured_DoesNotFailOverall()
    {
        var serverProbe = new FakeServerProbe(HealthStatus.Healthy);
        var dbProbe = new FakeDbProbe(HealthStatus.Healthy);
        var llmProbe = new FakeLlmProbe(HealthStatus.NotConfigured);

        var handler = new GetHealthHandler(new IHealthProbe[] { serverProbe, dbProbe, llmProbe });
        var result = await handler.HandleAsync();

        Assert.True(result.IsSuccess);
        Assert.NotEqual(HealthStatus.Unhealthy, result.Value.OverallStatus);
    }
}

// Uses HealthStatus, GetHealthHandler, IHealthProbe from HydraForge.Application.Health
// Uses IHealthProbe from HydraForge.Application.Health (defined in that project)

internal class FakeServerProbe : IHealthProbe
{
    private readonly HealthStatus _status;

    public FakeServerProbe(HealthStatus status) => _status = status;

    public Task<HealthStatus> CheckAsync(CancellationToken ct = default) => Task.FromResult(_status);
}

internal class FakeDbProbe : IHealthProbe
{
    private readonly HealthStatus _status;

    public FakeDbProbe(HealthStatus status) => _status = status;

    public Task<HealthStatus> CheckAsync(CancellationToken ct = default) => Task.FromResult(_status);
}

internal class FakeLlmProbe : IHealthProbe
{
    private readonly HealthStatus _status;

    public FakeLlmProbe(HealthStatus status) => _status = status;

    public Task<HealthStatus> CheckAsync(CancellationToken ct = default) => Task.FromResult(_status);
}