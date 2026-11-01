using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrbiApp.Presentation.Client;
using OrbiApp.Application.Features.Shared.Ports;
using OrbiApp.Models.Identity;

namespace OrbiApp.Presentation;

public static class WebApplicationExtensions
{
    public static WebApplication UseOrbiPresentation(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
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

        app.UseExceptionHandler("/errors/500");
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
            CancellationToken cancellationToken) =>
        {
            var publicRoles = new[] { "Usuario", "Vendedor", "Repartidor" };
            var roleNames = await roleManager.Roles
                .AsNoTracking()
                .Where(role => role.Name != null && publicRoles.Contains(role.Name))
                .Select(role => role.Name!)
                .ToListAsync(cancellationToken);
            var roles = publicRoles.Where(roleNames.Contains).ToArray();

            var provinces = await data.Set<EcuadorProvince>()
                .AsNoTracking()
                .OrderBy(province => province.Name)
                .Select(province => new RegistrationProvinceOption(province.Code, province.Name))
                .ToListAsync(cancellationToken);
            var cities = await data.Set<EcuadorCity>()
                .AsNoTracking()
                .OrderBy(city => city.Name)
                .Select(city => new RegistrationCityOption(city.Code, city.Name, city.ProvinceCode))
                .ToListAsync(cancellationToken);

            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new RegistrationOptionsResponse(tokens.RequestToken ?? string.Empty, roles, provinces, cities));
        }).AllowAnonymous().WithRequestTimeout("api");

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(_Imports).Assembly);
        app.MapFallbackToController("Status", "Errors");

        return app;
    }
}
