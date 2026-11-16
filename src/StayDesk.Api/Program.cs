using Microsoft.EntityFrameworkCore;
using Serilog;
using StayDesk.Api.Middleware;
using StayDesk.Infrastructure;
using StayDesk.Infrastructure.Persistence;
using StayDesk.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<StayDeskDbContext>();

var app = builder.Build();

await PrepareDatabaseAsync(app);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "StayDesk API v1"));
}

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

// Development applies migrations on startup; production runs them from CD (OPS-2).
// Seeding is a separate switch because the public demo needs it outside Development.
static async Task PrepareDatabaseAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();

    if (app.Environment.IsDevelopment())
    {
        await scope.ServiceProvider.GetRequiredService<StayDeskDbContext>().Database.MigrateAsync();
    }

    if (app.Configuration.GetValue<bool>($"{SeedOptions.Section}:{nameof(SeedOptions.DemoData)}"))
    {
        await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
    }
}
