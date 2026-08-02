namespace HydraForge.Application.Tests.Llm;

using HydraForge.Application.Admin;
using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Entities.Admin;
using NSubstitute;

public class LlmCallGuardTests
{
    private static (IUserTokenBudgetRepository repo, IUsageRecorder recorder) CreateMocks()
    {
        var repo = Substitute.For<IUserTokenBudgetRepository>();
        var recorder = Substitute.For<IUsageRecorder>();
        return (repo, recorder);
    }

    private static LlmCallGuard CreateGuard(IUserTokenBudgetRepository repo, IUsageRecorder recorder) =>
        new(repo, recorder);

    [Fact]
    public async Task CheckTokenBudgetAsync_UnderBudget_ReturnsSuccess()
    {
        var (repo, recorder) = CreateMocks();
        var userId = Guid.NewGuid();
        repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserTokenBudget
            {
                UserId = userId,
                MonthlyTokenBudget = 1000,
                MonthlyTokenUsed = 500,
                PeriodStart = DateTime.UtcNow,
                PeriodEnd = DateTime.UtcNow.AddMonths(1)
            });

        var guard = CreateGuard(repo, recorder);

        var result = await guard.CheckTokenBudgetAsync(userId, 200);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckTokenBudgetAsync_OverBudget_ReturnsTokenBudgetExceeded()
    {
        var (repo, recorder) = CreateMocks();
        var userId = Guid.NewGuid();
        repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserTokenBudget
            {
                UserId = userId,
                MonthlyTokenBudget = 1000,
                MonthlyTokenUsed = 900,
                PeriodStart = DateTime.UtcNow,
                PeriodEnd = DateTime.UtcNow.AddMonths(1)
            });

        var guard = CreateGuard(repo, recorder);

        var result = await guard.CheckTokenBudgetAsync(userId, 200);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.TokenBudgetExceeded, result.Error.Code);
    }

    [Fact]
    public async Task CheckImageBudgetAsync_OverBudget_ReturnsImageBudgetExceeded()
    {
        var (repo, recorder) = CreateMocks();
        var userId = Guid.NewGuid();
        repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserTokenBudget
            {
                UserId = userId,
                MonthlyImageBudget = 10,
                MonthlyImageUsed = 9,
                PeriodStart = DateTime.UtcNow,
                PeriodEnd = DateTime.UtcNow.AddMonths(1)
            });

        var guard = CreateGuard(repo, recorder);

        var result = await guard.CheckImageBudgetAsync(userId, 5);

        Assert.True(result.IsFailure);
        Assert.Equal(DomainErrorCodes.Llm.ImageBudgetExceeded, result.Error.Code);
    }

    [Fact]
    public async Task CheckTokenBudgetAsync_UnlimitedBudget_ReturnsSuccess()
    {
        var (repo, recorder) = CreateMocks();
        var userId = Guid.NewGuid();
        repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserTokenBudget
            {
                UserId = userId,
                MonthlyTokenBudget = 0,
                MonthlyTokenUsed = 999999,
                PeriodStart = DateTime.UtcNow,
                PeriodEnd = DateTime.UtcNow.AddMonths(1)
            });

        var guard = CreateGuard(repo, recorder);

        var result = await guard.CheckTokenBudgetAsync(userId, 1_000_000);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckTokenBudgetAsync_NoBudgetRow_ReturnsSuccess()
    {
        var (repo, recorder) = CreateMocks();
        repo.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((UserTokenBudget?)null);

        var guard = CreateGuard(repo, recorder);

        var result = await guard.CheckTokenBudgetAsync(Guid.NewGuid(), 1_000_000);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckTokenBudgetAsync_LazyRollover_ResetsCountersBeforeCheck()
    {
        var (repo, recorder) = CreateMocks();
        var userId = Guid.NewGuid();
        repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserTokenBudget
            {
                UserId = userId,
                MonthlyTokenBudget = 1000,
                MonthlyTokenUsed = 999,
                PeriodStart = DateTime.UtcNow.AddMonths(-2),
                PeriodEnd = DateTime.UtcNow.AddMonths(-1)
            });

        var guard = CreateGuard(repo, recorder);

        var result = await guard.CheckTokenBudgetAsync(userId, 500);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AccrueAfterCallAsync_UpdatesCounters()
    {
        var (repo, recorder) = CreateMocks();
        var userId = Guid.NewGuid();
        var usage = new UsageSnapshot(InputTokens: 100, OutputTokens: 50, CachedTokens: 10);
        recorder.AccrueTokenUsageAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(150);

        var guard = CreateGuard(repo, recorder);

        var result = await guard.AccrueAfterCallAsync(userId, usage);

        Assert.Equal(150, result);
        await recorder.Received(1).AccrueTokenUsageAsync(userId, 150, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckImageBudgetAsync_UnderBudget_ReturnsSuccess()
    {
        var (repo, recorder) = CreateMocks();
        var userId = Guid.NewGuid();
        repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserTokenBudget
            {
                UserId = userId,
                MonthlyImageBudget = 10,
                MonthlyImageUsed = 3,
                PeriodStart = DateTime.UtcNow,
                PeriodEnd = DateTime.UtcNow.AddMonths(1)
            });

        var guard = CreateGuard(repo, recorder);

        var result = await guard.CheckImageBudgetAsync(userId, 2);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckImageBudgetAsync_UnlimitedBudget_ReturnsSuccess()
    {
        var (repo, recorder) = CreateMocks();
        var userId = Guid.NewGuid();
        repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserTokenBudget
            {
                UserId = userId,
                MonthlyImageBudget = 0,
                MonthlyImageUsed = 999,
                PeriodStart = DateTime.UtcNow,
                PeriodEnd = DateTime.UtcNow.AddMonths(1)
            });

        var guard = CreateGuard(repo, recorder);

        var result = await guard.CheckImageBudgetAsync(userId, 1000);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckImageBudgetAsync_NoBudgetRow_ReturnsSuccess()
    {
        var (repo, recorder) = CreateMocks();
        repo.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((UserTokenBudget?)null);

        var guard = CreateGuard(repo, recorder);

        var result = await guard.CheckImageBudgetAsync(Guid.NewGuid(), 999);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckImageBudgetAsync_LazyRollover_ResetsCountersBeforeCheck()
    {
        var (repo, recorder) = CreateMocks();
        var userId = Guid.NewGuid();
        repo.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new UserTokenBudget
            {
                UserId = userId,
                MonthlyImageBudget = 10,
                MonthlyImageUsed = 9,
                PeriodStart = DateTime.UtcNow.AddMonths(-2),
                PeriodEnd = DateTime.UtcNow.AddMonths(-1)
            });

        var guard = CreateGuard(repo, recorder);

        var result = await guard.CheckImageBudgetAsync(userId, 5);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task AccrueAfterCallAsync_NullUsage_AccruesZero()
    {
        var (repo, recorder) = CreateMocks();
        var userId = Guid.NewGuid();
        recorder.AccrueTokenUsageAsync(userId, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(0);

        var guard = CreateGuard(repo, recorder);

        var result = await guard.AccrueAfterCallAsync(userId, null);

        Assert.Equal(0, result);
        await recorder.Received(1).AccrueTokenUsageAsync(userId, 0, Arg.Any<CancellationToken>());
    }
}
