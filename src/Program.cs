using OrbiApp.Data;
using OrbiApp.Infrastructure;
using OrbiApp.Presentation;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOrbiPresentation(builder.Configuration, builder.Environment);

var app = builder.Build();
await DatabaseInitializer.InitializeAsync(app.Services, app.Environment.ContentRootPath);
app.UseOrbiPresentation();
app.Run();
