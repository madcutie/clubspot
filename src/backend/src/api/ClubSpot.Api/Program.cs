using System.Text;
using ClubSpot.Api.Auth;
using ClubSpot.Api.Endpoints;
using ClubSpot.Api.Errors;
using ClubSpot.Api.Seed;
using ClubSpot.Api.Tenancy;
using ClubSpot.Application.Modularity;
using ClubSpot.Infrastructure.DependencyInjection;
using ClubSpot.Infrastructure.Persistence;
using ClubSpot.SharedKernel.Modularity;
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
builder.Services.AddClubSpotModularity();
builder.Services.AddSingleton(new ModuleCatalog([
    new CoreModule(), new MembersModule(), new FinanceModule(), new BookingsModule(), new PadelModule(), new FootballModule()
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
builder.Services.AddCors(options => options.AddPolicy("backoffice", policy =>
    policy.WithOrigins("http://localhost:5184").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddExceptionHandler<ModuleDisabledExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddScoped<DevSeeder>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
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

app.Run();

public partial class Program;
