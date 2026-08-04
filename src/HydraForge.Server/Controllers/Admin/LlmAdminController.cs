namespace HydraForge.Server.Controllers.Admin;

using HydraForge.Application.Llm;
using HydraForge.Domain.Common;
using HydraForge.Domain.Constants;
using HydraForge.Server.Auth;
using HydraForge.Server.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize(Policy = AuthPolicies.AdminRequired)]
[ApiController]
[Route("api/admin")]
public class LlmAdminController : ControllerBase
{
    private readonly ILlmAdminService _llmAdmin;

    public LlmAdminController(ILlmAdminService llmAdmin)
    {
        _llmAdmin = llmAdmin;
    }

    // Providers

    [HttpGet("providers")]
    [ProducesResponseType(typeof(ProviderPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProviders(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        [FromQuery] string? search = null,
        CancellationToken ct = default
    )
    {
        var result = await _llmAdmin.ListProvidersAsync(skip, take, search, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpPost("providers")]
    [ProducesResponseType(typeof(ProviderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProvider(
        [FromBody] CreateProviderInput input,
        CancellationToken ct
    )
    {
        var result = await _llmAdmin.CreateProviderAsync(input, ct);
        if (result.IsFailure)
        {
            return ToProblemResult(result.Error);
        }

        var dto = result.Value;
        return CreatedAtAction(nameof(GetProvider), new { id = dto.Id }, dto);
    }

    [HttpGet("providers/{id:guid}")]
    [ProducesResponseType(typeof(ProviderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProvider(Guid id, CancellationToken ct)
    {
        var result = await _llmAdmin.GetProviderAsync(id, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpPut("providers/{id:guid}")]
    [ProducesResponseType(typeof(ProviderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProvider(
        Guid id,
        [FromBody] UpdateProviderInput input,
        CancellationToken ct
    )
    {
        var result = await _llmAdmin.UpdateProviderAsync(id, input, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpDelete("providers/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DisableProvider(Guid id, CancellationToken ct)
    {
        var result = await _llmAdmin.DisableProviderAsync(id, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : NoContent();
    }

    [HttpDelete("providers/{id:guid}/permanent")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProviderPermanently(Guid id, CancellationToken ct)
    {
        var result = await _llmAdmin.PermanentlyDeleteProviderAsync(id, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : NoContent();
    }

    // Models

    [HttpGet("providers/{providerId:guid}/models")]
    [ProducesResponseType(typeof(IReadOnlyList<ProviderModelDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ProbeModels(Guid providerId, CancellationToken ct)
    {
        var result = await _llmAdmin.ProbeModelsAsync(providerId, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpGet("providers/{providerId:guid}/models/configured")]
    [ProducesResponseType(typeof(IReadOnlyList<ProviderModelConfigDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListModels(Guid providerId, CancellationToken ct)
    {
        var result = await _llmAdmin.ListModelsAsync(providerId, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpPost("providers/{providerId:guid}/models")]
    [ProducesResponseType(typeof(ProviderModelConfigDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateModel(
        Guid providerId,
        [FromBody] CreateModelInput input,
        CancellationToken ct
    )
    {
        var result = await _llmAdmin.CreateModelAsync(providerId, input, ct);
        if (result.IsFailure)
        {
            return ToProblemResult(result.Error);
        }

        var dto = result.Value;
        return CreatedAtAction(nameof(GetModel), new { providerId, modelId = dto.Id }, dto);
    }

    [HttpGet("providers/{providerId:guid}/models/{modelId:guid}")]
    [ProducesResponseType(typeof(ProviderModelConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModel(Guid providerId, Guid modelId, CancellationToken ct)
    {
        var result = await _llmAdmin.GetModelAsync(providerId, modelId, ct);
        if (result.IsFailure)
        {
            return ToProblemResult(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPut("providers/{providerId:guid}/models/{modelId:guid}")]
    [ProducesResponseType(typeof(ProviderModelConfigDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateModel(
        Guid providerId,
        Guid modelId,
        [FromBody] UpdateModelInput input,
        CancellationToken ct
    )
    {
        var result = await _llmAdmin.UpdateModelAsync(providerId, modelId, input, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpDelete("providers/{providerId:guid}/models/{modelId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteModel(
        Guid providerId,
        Guid modelId,
        CancellationToken ct
    )
    {
        var result = await _llmAdmin.DeleteModelAsync(providerId, modelId, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : NoContent();
    }

    // Routing

    [HttpGet("routing")]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureRoutingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRouting(CancellationToken ct)
    {
        var result = await _llmAdmin.ListRoutingAsync(ct);
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpPut("routing/{feature}")]
    [ProducesResponseType(typeof(FeatureRoutingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRouting(
        string feature,
        [FromBody] UpdateRoutingInput input,
        CancellationToken ct
    )
    {
        var result = await _llmAdmin.UpdateRoutingAsync(feature, input, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpPut("routing/{feature}/allowed-models")]
    [ProducesResponseType(typeof(FeatureRoutingDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetAllowedModels(
        string feature,
        [FromBody] SetAllowedModelsInput input,
        CancellationToken ct
    )
    {
        var result = await _llmAdmin.SetAllowedModelsAsync(feature, input, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    // Usage

    [HttpGet("usage/tokens")]
    [ProducesResponseType(typeof(TokenUsagePageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryTokenUsage(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? projectId,
        [FromQuery] List<string>? feature,
        [FromQuery] Guid? providerId,
        [FromQuery] string? modelId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default
    )
    {
        take = Math.Min(take, PaginationConstants.MaxAdminPageSize);
        var result = await _llmAdmin.QueryTokenUsageAsync(
            userId,
            projectId,
            feature,
            providerId,
            modelId,
            from,
            to,
            skip,
            take,
            ct
        );
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpGet("usage/images")]
    [ProducesResponseType(typeof(ImageUsagePageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryImageUsage(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? projectId,
        [FromQuery] List<string>? feature,
        [FromQuery] Guid? providerId,
        [FromQuery] string? modelId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default
    )
    {
        take = Math.Min(take, PaginationConstants.MaxAdminPageSize);
        var result = await _llmAdmin.QueryImageUsageAsync(
            userId,
            projectId,
            feature,
            providerId,
            modelId,
            from,
            to,
            skip,
            take,
            ct
        );
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    // Budget

    [HttpGet("users/{userId:guid}/budget")]
    [ProducesResponseType(typeof(UserBudgetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBudget(Guid userId, CancellationToken ct)
    {
        var result = await _llmAdmin.GetBudgetAsync(userId, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    [HttpPut("users/{userId:guid}/budget")]
    [ProducesResponseType(typeof(UserBudgetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateBudget(
        Guid userId,
        [FromBody] UpdateBudgetInput input,
        CancellationToken ct
    )
    {
        var result = await _llmAdmin.UpdateBudgetAsync(userId, input, ct);
        return result.IsFailure ? ToProblemResult(result.Error) : Ok(result.Value);
    }

    private ObjectResult ToProblemResult(Error error)
    {
        var correlationId =
            HttpContext.Items["CorrelationId"] as string ?? HttpContext.TraceIdentifier;
        var problemDetails = ProblemDetailsMapper.FromError(error, correlationId);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
