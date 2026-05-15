using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace BoatSpotFinder.Web.Infrastructure;

public class HangfireAdminAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize([NotNull] DashboardContext context)
    {
        return context.GetHttpContext().User.IsInRole("Admin");
    }
}
