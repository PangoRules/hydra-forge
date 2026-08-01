namespace HydraForge.Infrastructure.Tests.Llm;

using HydraForge.Application.Llm;
using HydraForge.Domain.Entities.Admin;
using HydraForge.Domain.Enums;
using HydraForge.Infrastructure.Llm;
using HydraForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

public class EfUsageRecorderTests
{
    private static DbContextOptions<HydraForgeDbContext> CreateOptions(string? connectionString)
    {
        var connString =
            connectionString
            ?? "Host=localhost;Database=hydraforge_test;Username=postgres;Password=password";

        return new DbContextOptionsBuilder<HydraForgeDbContext>()
            .UseNpgsql(connString, o => o.UseVector())
            .Options;
    }

    [Fact]
    public async Task RecordTokenAsync_PersistsRecordWithCorrectFields()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var recorder = new EfUsageRecorder(context);

        var input = new TokenUsageRecordInput(
            UserId: Guid.NewGuid(),
            ProjectId: Guid.NewGuid(),
            Feature: AiFeature.ProjectChat,
            ProviderModelConfigId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            ModelId: "gpt-4o",
            ModelName: "GPT-4o",
            InputTokens: 100,
            OutputTokens: 50,
            CachedTokens: 10,
            PipelineRunId: null,
            Cost: 0m
        );

        await recorder.RecordTokenAsync(input);
        await context.SaveChangesAsync();

        var record = context.TokenUsageRecords.Single(r => r.UserId == input.UserId);
        Assert.Equal(input.UserId, record.UserId);
        Assert.Equal(input.ProjectId, record.ProjectId);
        Assert.Equal(AiFeature.ProjectChat, record.Feature);
        Assert.Equal(input.ProviderModelConfigId, record.ProviderModelConfigId);
        Assert.Equal(input.ProviderId, record.ProviderId);
        Assert.Equal("gpt-4o", record.ModelId);
        Assert.Equal("GPT-4o", record.ModelName);
        Assert.Equal(100, record.InputTokens);
        Assert.Equal(50, record.OutputTokens);
        Assert.Equal(10, record.CachedTokens);
    }

    [Fact]
    public async Task RecordImageAsync_PersistsRecordWithCorrectFields()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var recorder = new EfUsageRecorder(context);

        var input = new ImageUsageRecordInput(
            UserId: Guid.NewGuid(),
            ProjectId: Guid.NewGuid(),
            Feature: AiFeature.ImageChat,
            ProviderModelConfigId: Guid.NewGuid(),
            ProviderId: Guid.NewGuid(),
            ModelId: "dall-e-3",
            ModelName: "DALL-E 3",
            ImageCount: 2,
            Resolution: "1024x1024",
            Cost: 0.04m
        );

        await recorder.RecordImageAsync(input);
        await context.SaveChangesAsync();

        var record = context.ImageUsageRecords.Single(r => r.UserId == input.UserId);
        Assert.Equal(input.UserId, record.UserId);
        Assert.Equal(input.ProjectId, record.ProjectId);
        Assert.Equal(AiFeature.ImageChat, record.Feature);
        Assert.Equal("dall-e-3", record.ModelId);
        Assert.Equal(2, record.ImageCount);
        Assert.Equal("1024x1024", record.Resolution);
        Assert.Equal(0.04m, record.Cost);
    }

    [Fact]
    public async Task AccrueTokenUsageAsync_IncrementsMonthlyTokenUsed()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var userId = Guid.NewGuid();

        context.UserTokenBudgets.Add(new UserTokenBudget
        {
            UserId = userId,
            MonthlyTokenBudget = 10000,
            MonthlyTokenUsed = 100,
            MonthlyImageUsed = 0,
            PeriodStart = DateTime.UtcNow,
            PeriodEnd = DateTime.UtcNow.AddMonths(1),
        });
        await context.SaveChangesAsync();

        var recorder = new EfUsageRecorder(context);
        var result = await recorder.AccrueTokenUsageAsync(userId, 50);

        Assert.Equal(150, result);
        Assert.Equal(150, context.UserTokenBudgets.Single(b => b.UserId == userId).MonthlyTokenUsed);
    }

    [Fact]
    public async Task AccrueImageUsageAsync_IncrementsMonthlyImageUsed()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var userId = Guid.NewGuid();

        context.UserTokenBudgets.Add(new UserTokenBudget
        {
            UserId = userId,
            MonthlyTokenBudget = 10000,
            MonthlyTokenUsed = 0,
            MonthlyImageUsed = 5,
            PeriodStart = DateTime.UtcNow,
            PeriodEnd = DateTime.UtcNow.AddMonths(1),
        });
        await context.SaveChangesAsync();

        var recorder = new EfUsageRecorder(context);
        var result = await recorder.AccrueImageUsageAsync(userId, 3);

        Assert.Equal(8, result);
        Assert.Equal(8, context.UserTokenBudgets.Single(b => b.UserId == userId).MonthlyImageUsed);
    }

    [Fact]
    public async Task AccrueTokenUsageAsync_LazyRollover_ResetsCountersAndAdvancesPeriod()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var userId = Guid.NewGuid();
        var oldPeriodEnd = DateTime.UtcNow.AddDays(-1);

        context.UserTokenBudgets.Add(new UserTokenBudget
        {
            UserId = userId,
            MonthlyTokenBudget = 10000,
            MonthlyTokenUsed = 500,
            MonthlyImageUsed = 10,
            PeriodStart = oldPeriodEnd.AddMonths(-1),
            PeriodEnd = oldPeriodEnd,
        });
        await context.SaveChangesAsync();

        var recorder = new EfUsageRecorder(context);
        var result = await recorder.AccrueTokenUsageAsync(userId, 25);

        var budget = context.UserTokenBudgets.Single(b => b.UserId == userId);
        Assert.Equal(25, result);
        Assert.Equal(25, budget.MonthlyTokenUsed);
        Assert.Equal(0, budget.MonthlyImageUsed);
        Assert.True(budget.PeriodEnd > oldPeriodEnd);
    }

    [Fact]
    public async Task AccrueTokenUsageAsync_AutoCreatesBudget_WhenNotExists()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var userId = Guid.NewGuid();

        var recorder = new EfUsageRecorder(context);
        var result = await recorder.AccrueTokenUsageAsync(userId, 100);

        Assert.Equal(100, result);
        var budget = context.UserTokenBudgets.Single(b => b.UserId == userId);
        Assert.Equal(userId, budget.UserId);
        Assert.Equal(0, budget.MonthlyTokenBudget);
        Assert.Equal(100, budget.MonthlyTokenUsed);
        Assert.Equal(0, budget.MonthlyImageUsed);
        Assert.True(budget.PeriodEnd > DateTime.UtcNow);
    }

    [Fact]
    public async Task RecordTokenAsync_ComputesCostFromPricePerToken()
    {
        string? connectionString = Environment.GetEnvironmentVariable("HYDRAFORGE_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var options = CreateOptions(connectionString);
        using var context = new HydraForgeDbContext(options);
        var configId = Guid.NewGuid();

        context.ProviderModelConfigs.Add(new ProviderModelConfig
        {
            Id = configId,
            ProviderId = Guid.NewGuid(),
            ModelId = "gpt-4o",
            Name = "GPT-4o",
            PricePerToken = 0.00001m,
        });
        await context.SaveChangesAsync();

        var recorder = new EfUsageRecorder(context);
        var input = new TokenUsageRecordInput(
            UserId: Guid.NewGuid(),
            ProjectId: null,
            Feature: AiFeature.PersonalChat,
            ProviderModelConfigId: configId,
            ProviderId: Guid.NewGuid(),
            ModelId: "gpt-4o",
            ModelName: "GPT-4o",
            InputTokens: 1000,
            OutputTokens: 500,
            CachedTokens: 200,
            PipelineRunId: null,
            Cost: 0m
        );

        await recorder.RecordTokenAsync(input);
        await context.SaveChangesAsync();

        var record = context.TokenUsageRecords.Single(r => r.UserId == input.UserId);
        var expectedCost = (1000 + 500 - 200) * 0.00001m;
        Assert.Equal(expectedCost, record.Cost);
    }
}
