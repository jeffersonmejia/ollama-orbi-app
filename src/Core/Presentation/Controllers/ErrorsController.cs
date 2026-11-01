using Microsoft.AspNetCore.Authorization;
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
        if (statusCode is not (400 or 401 or 403 or 404 or 408 or 429 or 500 or 502 or 503 or 504))
            statusCode = 500;

        Response.StatusCode = statusCode;
        return View("Status", statusCode);
    }
}
