using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using SakilaApp.Data;
using SakilaApp.Services;
using SakilaApp.Settings;
using SakilaApp.Services.Payments;

// Orbi opera en dólares estadounidenses. Una cultura monetaria explícita evita
// que Linux muestre el símbolo genérico ¤ cuando las vistas usan formato "C".
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("es-EC");

var builder = WebApplication.CreateBuilder(args);

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    builder.Services
        .AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
        .SetApplicationName("OrbiApp");
}

builder.Services.Configure<PayPhoneSettings>(
    builder.Configuration.GetSection("PayPhone"));

builder.Services.AddHttpClient<PayPhoneApiLinkService>();
builder.Services.AddHttpClient<OllamaProductService>(client =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(45);
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDbContext<SakilaContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
        // El dominio Orbi se inicializa con scripts SQL versionados, fuera de las
        // migraciones Identity heredadas del proyecto base.
        .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));

builder.Services.Configure<PayPalSettings>(
    builder.Configuration.GetSection("PayPal"));

builder.Services.AddHttpClient<PayPalService>();

builder.Services
    .AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequiredLength = 6;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddTransient<IEmailSender<IdentityUser>, GmailEmailSender>();
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, GmailEmailSender>();
builder.Services.AddHostedService<EmailQueueWorker>();

builder.Services
    .AddAuthentication()
    .AddGoogle(options =>
    {
        options.SignInScheme = IdentityConstants.ExternalScheme;
        options.CallbackPath = "/signin-google";
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.Events.OnRemoteFailure = context =>
        {
            context.HandleResponse();
            context.Response.Redirect("/Identity/Account/Login?oauthError=google");
            return Task.CompletedTask;
        };

        if (builder.Environment.IsDevelopment())
        {
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        }
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var identityDb = sp.GetRequiredService<ApplicationDbContext>();
    await identityDb.Database.MigrateAsync();

    var sakilaDb = sp.GetRequiredService<SakilaContext>();
    var conn = sakilaDb.Database.GetDbConnection();
    await conn.OpenAsync();
    using var cmd = conn.CreateCommand();

    foreach (var fileName in new[] { "orbi-schema.sql", "orbi-locations.sql" })
    {
        var sqlPath = Path.Combine(Environment.CurrentDirectory, "db", fileName);
        if (File.Exists(sqlPath))
        {
            cmd.CommandText = await File.ReadAllTextAsync(sqlPath);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    await IdentitySeeder.SeedAsync(sp);

    var dataPath = Path.Combine(Environment.CurrentDirectory, "db", "orbi-data.sql");
    if (File.Exists(dataPath))
    {
        cmd.CommandText = await File.ReadAllTextAsync(dataPath);
        await cmd.ExecuteNonQueryAsync();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
