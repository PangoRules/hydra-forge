using HydraForge.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace HydraForge.Server.Errors;

public static class ProblemDetailsResultExtensions
{
    public static ObjectResult ToProblemResult(this ControllerBase controller, Error error)
    {
        var correlationId =
            controller.HttpContext.Items["CorrelationId"] as string
            ?? controller.HttpContext.TraceIdentifier;
        var problemDetails = ProblemDetailsMapper.FromError(error, correlationId);

        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status,
            ContentTypes = { "application/problem+json" },
        };
    }
}
