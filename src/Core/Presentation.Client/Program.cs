using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using OrbiApp.Presentation.Client.Networking;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthenticationStateDeserialization();
builder.Services.Configure<ApiTimeoutOptions>(builder.Configuration.GetSection("Blazor"));
builder.Services.AddTransient<ApiTimeoutHandler>();
builder.Services.AddHttpClient("OrbiApi", (services, client) =>
{
    var options = services.GetRequiredService<IOptions<ApiTimeoutOptions>>().Value;
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.HttpClientTimeoutSeconds, 2, 600));
}).AddHttpMessageHandler<ApiTimeoutHandler>();
builder.Services.AddScoped(services => services.GetRequiredService<IHttpClientFactory>().CreateClient("OrbiApi"));

await builder.Build().RunAsync();
