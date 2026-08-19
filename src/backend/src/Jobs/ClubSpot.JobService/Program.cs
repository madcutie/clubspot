using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ClubSpot.Infrastructure.DependencyInjection;
using ClubSpot.Infrastructure.MercadoPago;
using ClubSpot.JobService;
using ClubSpot.SharedKernel.Time;
using ClubSpot.Application.Modularity;
using ClubSpot.SharedKernel.Modularity;
using Hangfire;
using Hangfire.PostgreSql;
using Npgsql;

var builder = Host.CreateApplicationBuilder(args);

var clubSpotConnection = builder.Configuration.GetConnectionString("ClubSpot")
    ?? throw new InvalidOperationException("Connection string 'ClubSpot' is required.");
var hangfireConnection = builder.Configuration.GetConnectionString("Hangfire")
    ?? throw new InvalidOperationException("Connection string 'Hangfire' is required.");

// Hangfire keeps its state in its own database (user decision, 17/08/2026); create it if missing.
EnsureDatabaseExists(hangfireConnection);

builder.Services.AddClubSpotTenancy();
builder.Services.AddClubSpotPersistence(clubSpotConnection);
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
builder.Services.AddSingleton<PaymentsReconciliationDispatcher>();

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(hangfireConnection)));
builder.Services.AddHangfireServer();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<IRecurringJobManager>().AddOrUpdate<PaymentsReconciliationDispatcher>(
        "payments-reconciliation",
        dispatcher => dispatcher.RunAsync(CancellationToken.None),
        "*/5 * * * *");
}

host.Run();

static void EnsureDatabaseExists(string connectionString)
{
    var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString);
    var database = connectionBuilder.Database
        ?? throw new InvalidOperationException("The Hangfire connection string needs a database name.");
    connectionBuilder.Database = "postgres";

    using var connection = new NpgsqlConnection(connectionBuilder.ConnectionString);
    connection.Open();
    using var exists = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = @name", connection);
    exists.Parameters.AddWithValue("name", database);
    if (exists.ExecuteScalar() is null)
    {
        using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
        create.ExecuteNonQuery();
    }
}
