using Microsoft.AspNetCore.Antiforgery;
using OrbiApp.Presentation.Client;

namespace OrbiApp.Presentation;

public static class WebApplicationExtensions
{
    public static WebApplication UseOrbiPresentation(this WebApplication app)
    {
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

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(typeof(_Imports).Assembly);
        app.MapFallbackToController("Status", "Errors");

        return app;
    }
}
