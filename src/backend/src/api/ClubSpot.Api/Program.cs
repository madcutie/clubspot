using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClubSpot.Api.Activity;
using ClubSpot.Api.Auth;
using ClubSpot.Api.Endpoints;
using ClubSpot.Api.Errors;
using ClubSpot.Api.OpenApi;
using ClubSpot.Api.Seed;
using ClubSpot.Api.Tenancy;
using ClubSpot.Application.Core;
using ClubSpot.Application.Modularity;
using ClubSpot.Domain.Bookings;
using ClubSpot.Domain.Core.People;
using ClubSpot.Infrastructure.DependencyInjection;
using ClubSpot.Infrastructure.MercadoPago;
using ClubSpot.Infrastructure.Payments;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Modularity;
using ClubSpot.SharedKernel.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("ClubSpot")
    ?? throw new InvalidOperationException("Connection string 'ClubSpot' is required.");
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is required.");
if (string.IsNullOrWhiteSpace(jwtOptions.Issuer) || string.IsNullOrWhiteSpace(jwtOptions.Audience) || jwtOptions.SigningKey.Length < 32)
    throw new InvalidOperationException("JWT configuration is invalid.");

builder.Services.AddClubSpotTenancy();
builder.Services.AddClubSpotPersistence(connectionString);
builder.Services.AddClubSpotAuth();
builder.Services.AddClubSpotPeople();
builder.Services.AddClubSpotBookings();
builder.Services.AddClubSpotPayments(builder.Configuration);
// Fail here rather than at the first booking: with a gateway wired, the portal hands the provider a
// return url and that url is now checked against this list. An empty list with a gateway configured
// is not a safe default, it is online booking that refuses every request.
if (builder.Configuration["Payments:Provider"] is { Length: > 0 } and not "none"
    && builder.Configuration.GetSection($"{PaymentsOptions.SectionName}:{nameof(PaymentsOptions.AllowedReturnOrigins)}")
        .Get<string[]>() is not { Length: > 0 })
{
    throw new InvalidOperationException(
        "Payments:AllowedReturnOrigins must list the portal's origin when a payment provider is configured.");
}
if (builder.Configuration["Payments:Provider"] == MercadoPagoProvider.ProviderName)
    builder.Services.AddClubSpotMercadoPago(builder.Configuration);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddClubSpotModularity();
builder.Services.AddSingleton(new ModuleCatalog([
    new CoreModule(), new MembersModule(), new FinanceModule(), new BookingsModule()
]));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<JwtIssuer>();
builder.Services.AddSingleton<SignInThrottle>();
builder.Services.AddSingleton<ClubSpot.Api.Endpoints.PortalBookingToken>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // What travels is what gets read: no implicit claim mapping in either direction (ADR-0018).
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            NameClaimType = ClubSpotClaims.Name,
            RoleClaimType = ClubSpotClaims.Role
        };
    });
builder.Services.AddClubSpotAuthorization();
// One converter per enum on purpose: the open generic would also camelCase enum dictionary keys,
// changing the day names of a schedule's weekly ranges.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<PersonOrigin>(JsonNamingPolicy.CamelCase));
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<Sport>(JsonNamingPolicy.CamelCase));
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<BookingStatus>(JsonNamingPolicy.CamelCase));
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<PaymentMode>(JsonNamingPolicy.CamelCase));
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<PaymentOutcome>(JsonNamingPolicy.CamelCase));
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<ClubSpot.Application.Bookings.PaymentApplyOutcome>(JsonNamingPolicy.CamelCase));
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<PaymentKind>(JsonNamingPolicy.CamelCase));
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<PaymentStatus>(JsonNamingPolicy.CamelCase));
});
// Behind a reverse proxy every request arrives from the proxy, so the rate limits below would all
// fall into one partition and stop limiting anyone. Configured and never assumed: honouring
// X-Forwarded-For without naming who may set it hands an attacker a fresh partition per request.
var trustedProxies = builder.Configuration.GetSection("Network:TrustedProxies").Get<string[]>() ?? [];
if (trustedProxies.Length > 0)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Clear();
        options.KnownIPNetworks.Clear();
        options.ForwardLimit = trustedProxies.Length;
        foreach (var proxy in trustedProxies)
        {
            if (IPAddress.TryParse(proxy, out var address)) options.KnownProxies.Add(address);
            else throw new InvalidOperationException($"Network:TrustedProxies has an invalid address: '{proxy}'.");
        }
    });
}
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (corsOrigins.Length == 0)
{
    // Production must name its own origins: inheriting the dev ports would mean the browser blocks
    // every call from the real domain, and the failure would show up as an empty screen at the
    // counter instead of a startup error.
    if (builder.Environment.IsProduction())
        throw new InvalidOperationException("Cors:AllowedOrigins is required outside development.");
    corsOrigins = ["http://localhost:5184", "http://localhost:5183"];
}
builder.Services.AddCors(options => options.AddPolicy("backoffice", policy =>
    policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));
// The portal is anonymous, so the only thing standing between a script and the club's whole agenda
// is this: holding a slot must cost the caller something. Reads stay generous, taking a slot does not.
// Partitioned by caller and club so one club's traffic never starves another's.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(PortalRateLimits.Reads, PortalRateLimits.PerCallerAndClub(permitPerMinute: 120));
    options.AddPolicy(PortalRateLimits.Bookings, PortalRateLimits.PerCallerAndClub(permitPerMinute: 10));
});
builder.Services.AddOpenApi(OpenApiExport.DocumentName, options =>
{
    options.OpenApiVersion = OpenApiExport.SpecVersion;
    OpenApiSchemaNormalizer.Apply(options);
});
var exportOpenApiPath = builder.Configuration[OpenApiExport.ArgumentName];
if (!string.IsNullOrWhiteSpace(exportOpenApiPath)) OpenApiExport.UseSilentServer(builder.Services);
builder.Services.AddExceptionHandler<ModuleDisabledExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<DevSeeder>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<ClubSpotDbContext>();
    await db.Database.MigrateAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<DevSeeder>();
    await seeder.SeedAsync();
}

// Before anything that reads the caller's address, which is what the rate limits partition on.
if (trustedProxies.Length > 0) app.UseForwardedHeaders();
app.UseExceptionHandler();
// Liveness: the process answers. Deliberately touches nothing else, so a database blip never
// makes the orchestrator kill a container that is perfectly able to serve.
app.MapGet("/health", () => TypedResults.NoContent()).AllowAnonymous().ExcludeFromDescription();
// Readiness: the database answers too. This is what tells a deploy it can take traffic, and what
// catches a wrong connection string instead of letting every request 500 behind a green light.
app.MapGet("/health/ready", async (IDatabaseProbe probe, CancellationToken cancellationToken) =>
        await probe.CanConnectAsync(cancellationToken)
            ? Results.NoContent()
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
    .AllowAnonymous().ExcludeFromDescription();
app.UseCors("backoffice");
// After CORS so a preflight never spends a permit, before the endpoints it protects.
app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<ActivityActorMiddleware>();
app.UseAuthorization();
app.MapAuth();
app.MapContext();
app.MapSchedules();
app.MapCourts();
app.MapAvailabilityOverrides();
app.MapBookings();
app.MapPortal();
app.MapPayments();
app.MapPeople();
// The contract is a build output (ADR-0016). Serving it from production would publish the whole
// map of the API, payment routes included, for nothing anyone needs at runtime.
if (!app.Environment.IsProduction()) app.MapOpenApi();
if (app.Environment.IsDevelopment()) app.MapDevCheckout();

if (!string.IsNullOrWhiteSpace(exportOpenApiPath))
{
    await OpenApiExport.WriteAsync(app, exportOpenApiPath);
    return;
}

app.Run();

public partial class Program;
