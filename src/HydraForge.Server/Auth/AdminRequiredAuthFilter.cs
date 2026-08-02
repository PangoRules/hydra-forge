using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace HydraForge.Server.Auth;

public class AdminRequiredAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.IsInRole("Admin");
    }
}
