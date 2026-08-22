using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace ClubSpot.Infrastructure.Observability;

// Diagnostics, not the chronicle. What the business did goes to the activityLog (ADR-0017), which is
// read to operate; this is read to find out why something broke, and it is allowed to disappear.
public static class ClubSpotLogging
{
    // Levels and extra sinks come from the "Serilog" section, which is that library's own contract.
    // This one key is ours, so it does not sit inside a section another library parses.
    public const string DirectoryKey = "Diagnostics:LogDirectory";

    // JSON everywhere it is machine-read: a hosting provider collects the container's stdout, and a
    // line whose fields are fields can be filtered by tenant instead of grepped. Development also
    // keeps a rolling file, because that is the only environment whose filesystem survives a restart.
    public static void AddClubSpotLogging(this IHostApplicationBuilder builder, string application,
        params ILogEventEnricher[] enrichers)
    {
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Hangfire", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("application", application)
            .Enrich.With(enrichers);

        if (builder.Environment.IsDevelopment())
        {
            // A sink that cannot write fails silently by design: Serilog routes its own errors here and
            // nowhere else. Without this an unwritable log directory looks exactly like a quiet system.
            SelfLog.Enable(Console.Error);
            logger.WriteTo.Console(outputTemplate:
                "{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}: {Message:lj}{NewLine}{Exception}");
            logger.WriteTo.File(new CompactJsonFormatter(),
                Path.Combine(FileDirectory(builder), $"{application}-.jsonl"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7,
                fileSizeLimitBytes: 64 * 1024 * 1024, rollOnFileSizeLimit: true, shared: true);
        }
        else
        {
            logger.WriteTo.Console(new CompactJsonFormatter());
        }

        // Applied last so a deployment can raise or lower a level without a rebuild, which is the
        // knob that keeps a chatty namespace from filling a retention window.
        logger.ReadFrom.Configuration(builder.Configuration);

        Log.Logger = logger.CreateLogger();
        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger, dispose: true);
        InstallCrashHandler();
    }

    private static int crashHandlerInstalled;

    // Everything that validates configuration throws before the host is built, and ClearProviders has
    // already removed the console provider that would have printed it — so without this a missing
    // connection string dies as a plain-text .NET dump that a JSON log collector cannot parse. Hooked
    // on the AppDomain rather than wrapping each Program.cs in try/catch on purpose: the test host
    // aborts startup with a sentinel exception it expects to swallow, and a catch there would file it
    // as a fatal crash and flush a logger the test is still using.
    private static void InstallCrashHandler()
    {
        if (Interlocked.Exchange(ref crashHandlerInstalled, 1) == 1) return;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
                Log.Fatal(exception, "The host terminated unexpectedly.");
            Log.CloseAndFlush();
        };
    }

    private static string FileDirectory(IHostApplicationBuilder builder)
    {
        var configured = builder.Configuration[DirectoryKey];
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(builder.Environment.ContentRootPath, "logs")
            : configured;
    }
}
