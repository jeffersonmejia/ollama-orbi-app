using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;
using OrbiApp.Presentation.Client;
using OrbiApp.Presentation.Logging;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Models.Identity;
using OrbiApp.Presentation.Services;
using Serilog.Context;

namespace OrbiApp.Presentation;

public static class WebApplicationExtensions
{
    public static WebApplication UseOrbiPresentation(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            using (LogContext.PushProperty("LogArea", LogAreaResolver.FromPath(context.Request.Path)))
            using (LogContext.PushProperty("TraceId", context.TraceIdentifier))
            using (LogContext.PushProperty("RequestPath", context.Request.Path.Value ?? "/"))
            {
                await next();
            }
        });

        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value;
            var isDeliveryCatalog = HttpMethods.IsGet(context.Request.Method) &&
                (string.Equals(path, "/Delivery", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(path, "/Delivery/Index", StringComparison.OrdinalIgnoreCase));
            if (isDeliveryCatalog)
            {
                var requestTimer = Stopwatch.StartNew();
                context.Response.OnCompleted(() =>
                {
                    app.Logger.LogInformation(
                        "HTTP timing {TraceId} GET {Path}: status {StatusCode}, total {ElapsedMs} ms",
                        context.TraceIdentifier,
                        path,
                        context.Response.StatusCode,
                        requestTimer.ElapsedMilliseconds);
                    return Task.CompletedTask;
                });
            }

            await next();
        });

        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";

                var contentSecurityPolicies = context.Response.Headers["Content-Security-Policy"];
                var hasFrameAncestors = contentSecurityPolicies.Any(policy =>
                    policy?.Split(';').Any(directive =>
                        directive.TrimStart().StartsWith("frame-ancestors", StringComparison.OrdinalIgnoreCase)) == true);

                if (!hasFrameAncestors)
                {
                    context.Response.Headers.Append("Content-Security-Policy", "frame-ancestors 'self'");
                }

                return Task.CompletedTask;
            });

            await next();
        });

        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseHsts();
        }

        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandlingPath = "/errors/exception",
            SuppressDiagnosticsCallback = _ => true
        });
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRequestTimeouts();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), branch =>
            branch.UseStatusCodePages(async statusContext =>
            {
                var response = statusContext.HttpContext.Response;
                response.Headers.CacheControl = "no-store";
                response.ContentType = "application/json; charset=utf-8";
                var message = response.StatusCode switch
                {
                    StatusCodes.Status401Unauthorized => "Inicia sesión para continuar.",
                    StatusCodes.Status403Forbidden => "No tienes permiso para realizar esta acción.",
                    StatusCodes.Status404NotFound => "No encontramos lo que buscas.",
                    StatusCodes.Status503ServiceUnavailable => "El servicio no está disponible por el momento.",
                    _ => "No se pudo completar la solicitud. Inténtalo más tarde."
                };
                await response.WriteAsJsonAsync(new { message }, statusContext.HttpContext.RequestAborted);
            }));

        app.UseWhen(context => !context.Request.Path.StartsWithSegments("/api"), branch =>
            branch.UseStatusCodePagesWithReExecute("/errors/{0}", createScopeForStatusCodePages: true));

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();
        app.MapGet("/api/security/antiforgery", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { requestToken = tokens.RequestToken });
        }).AllowAnonymous().WithRequestTimeout("api");

        app.MapGet("/api/identity/registration/options", async (
            HttpContext context,
            IAntiforgery antiforgery,
            RoleManager<IdentityRole> roleManager,
            IApplicationDataSession data,
            DeliveryPageCache pageCache,
            CancellationToken cancellationToken) =>
        {
            var publicRoles = new[] { "Usuario", "Vendedor", "Repartidor" };
            var roleNames = await roleManager.Roles
                .AsNoTracking()
                .Where(role => role.Name != null && publicRoles.Contains(role.Name))
                .Select(role => role.Name!)
                .ToListAsync(cancellationToken);
            var roles = publicRoles.Where(roleNames.Contains).ToArray();

            var provinces = (await pageCache.GetProvincesAsync(data, cancellationToken))
                .Select(province => new RegistrationProvinceOption(province.Code, province.Name))
                .ToList();
            var cities = (await pageCache.GetCitiesAsync(data, cancellationToken))
                .Select(city => new RegistrationCityOption(city.Code, city.Name, city.ProvinceCode))
                .ToList();

            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new RegistrationOptionsResponse(tokens.RequestToken ?? string.Empty, roles, provinces, cities));
        }).AllowAnonymous().WithRequestTimeout("api");

        app.MapStaticAssets();
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealthStatusAsync
        });
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthStatusAsync
        });
        app.MapRazorComponents<App>()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(_Imports).Assembly);
        app.MapFallbackToController("Status", "Errors");

        return app;
    }

    private static Task WriteHealthStatusAsync(HttpContext context, HealthReport report)
    {
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(new { status = report.Status.ToString() }, context.RequestAborted);
    }
}
