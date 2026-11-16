using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using StayDesk.Api;
using StayDesk.Api.Errors;
using StayDesk.Api.Middleware;
using StayDesk.Application;
using StayDesk.Infrastructure;
using StayDesk.Infrastructure.Identity;
using StayDesk.Infrastructure.Persistence;
using StayDesk.Infrastructure.Persistence.Seeding;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
    // FluentValidation is the one place request rules live; without this, MVC adds its own
    // [Required] errors for non-nullable strings before the validators run.
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
})
    // Enums are strings in the database (SRS §5), so the API uses the same names.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names as issued (`sub`, `role`) instead of mapping them to long WS-* URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

// NFR-3: 10 requests per minute per client IP on login, register and refresh.
// Configurable only so integration tests, which all share one client IP, can raise it.
var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Auth, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitLimit, Window = TimeSpan.FromMinutes(1) }));
});

// NFR-3: browsers may call the API only from the configured frontend origin(s).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    // Gives Swagger UI its Authorize button for the JWT access token.
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    };
    document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
    return Task.CompletedTask;
}));
builder.Services.AddHealthChecks().AddDbContextCheck<StayDeskDbContext>();

var app = builder.Build();

await PrepareDatabaseAsync(app);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "StayDesk API v1"));
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

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
