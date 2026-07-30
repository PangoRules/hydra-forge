using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.RateLimiting;

namespace HydraForge.Server.Conventions;

public class RateLimitConvention(string policyName) : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        // Skip controllers that already have explicit rate limiting
        if (controller.Attributes.OfType<EnableRateLimitingAttribute>().Any())
            return;

        // Skip health endpoint
        if (controller.ControllerType == typeof(Controllers.Health.HealthController))
            return;

        // EnableRateLimitingAttribute is plain metadata, not an IFilterMetadata, so it must
        // go on the selector's EndpointMetadata (what the rate limiter middleware reads from
        // the resolved endpoint) rather than controller.Filters.
        foreach (var selector in controller.Selectors)
        {
            selector.EndpointMetadata.Add(new EnableRateLimitingAttribute(policyName));
        }
    }
}
