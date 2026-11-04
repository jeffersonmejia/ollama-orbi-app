using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Presentation.Client.Networking;
using OrbiApp.Presentation.Features.Home;

namespace OrbiApp.Controllers;

[ApiController]
[Authorize(Roles = "Administrador")]
[Route("api/home")]
public sealed class HomeDashboardApiController(IApplicationDataSession data) : ControllerBase
{
    [HttpGet("admin-metrics")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<IReadOnlyList<AdminDashboardMetricDto>>> GetAdminMetrics(
        CancellationToken cancellationToken)
    {
        var metrics = await AdminDashboardMetricsQuery.GetAsync(data, cancellationToken);
        return Ok(metrics.Select(metric => new AdminDashboardMetricDto(
            metric.Label,
            metric.Detail,
            metric.Value,
            metric.Percent)).ToArray());
    }
}
