using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClubSpot.Api.Auth;
using ClubSpot.Api.Endpoints;
using ClubSpot.Api.Errors;
using ClubSpot.Api.Seed;
using ClubSpot.Api.Tenancy;
using ClubSpot.Application.Modularity;
using ClubSpot.Domain.Bookings;
using ClubSpot.Domain.Core;
using ClubSpot.Domain.Core.People;
using ClubSpot.Infrastructure.DependencyInjection;
using ClubSpot.Infrastructure.MercadoPago;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Modularity;
using ClubSpot.SharedKernel.Time;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
if (builder.Configuration["Payments:Provider"] == MercadoPagoProvider.ProviderName)
    builder.Services.AddClubSpotMercadoPago(builder.Configuration);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddClubSpotModularity();
builder.Services.AddSingleton(new ModuleCatalog([
    new CoreModule(), new MembersModule(), new FinanceModule(), new BookingsModule()
]));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<JwtIssuer>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true
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
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<ClubSpot.Application.Bookings.PaymentApplyOutcome>(JsonNamingPolicy.CamelCase));
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<Role>(JsonNamingPolicy.CamelCase));
});
builder.Services.AddCors(options => options.AddPolicy("backoffice", policy =>
    policy.WithOrigins("http://localhost:5184", "http://localhost:5183").AllowAnyHeader().AllowAnyMethod()));
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

app.UseExceptionHandler();
app.MapGet("/", () => "Hello World!");
app.UseCors("backoffice");
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
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
if (app.Environment.IsDevelopment()) app.MapDevCheckout();

app.Run();

public partial class Program;
