using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OrbiApp.Presentation.Controllers;

[AllowAnonymous]
[Route("errors")]
public sealed class ErrorsController : Controller
{
    [HttpGet("{statusCode:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Status(int statusCode = 404)
    {
        Response.Headers.CacheControl = "no-store";
        statusCode = statusCode is 401 or 403 or 404 or 408 or 429 or 500 or 502 or 503 or 504
            ? statusCode
            : 500;

        Response.StatusCode = statusCode;
        return View("Status", statusCode);
    }

    [HttpGet("exception")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Exception()
    {
        Response.Headers.CacheControl = "no-store";
        var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
        var statusCode = feature?.Error is null
            ? StatusCodes.Status500InternalServerError
            : GlobalExceptionHandler.GetStatusCode(feature.Error);
        Response.StatusCode = statusCode;

        if (feature?.Path?.StartsWith("/api", StringComparison.OrdinalIgnoreCase) == true)
        {
            var message = statusCode == StatusCodes.Status503ServiceUnavailable
                ? "El servicio no está disponible por el momento."
                : "No se pudo completar la solicitud. Inténtalo más tarde.";
            return Json(new { message });
        }

        return View("Status", statusCode);
    }
}
