using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ClubSpot.Api.OpenApi;

// The contract is a build output, not something a running server has to be up to serve: the build
// maps every route, asks for the same document MapOpenApi would return, and leaves (ADR-0016).
internal static class OpenApiExport
{
    public const string DocumentName = "v1";
    public const string ArgumentName = "export-openapi";

    // 3.1 and not 3.0: downgrading flattens a nullable enum property to a bare string, which
    // is exactly the hand-written union this contract exists to replace.
    public const OpenApiSpecVersion SpecVersion = OpenApiSpecVersion.OpenApi3_1;

    public static void UseSilentServer(IServiceCollection services) => services.AddSingleton<IServer, SilentServer>();

    public static async Task WriteAsync(WebApplication app, string path)
    {
        // Starting the host is what moves the mapped routes into the endpoint data sources the
        // document is built from; without it the document comes out empty. Nothing listens: the
        // server was replaced before Build so a compilation never opens a port.
        await app.StartAsync();
        try
        {
            var provider = app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(DocumentName);
            var document = await provider.GetOpenApiDocumentAsync(CancellationToken.None);

            using var buffer = new MemoryStream();
            await document.SerializeAsJsonAsync(buffer, SpecVersion, CancellationToken.None);
            // Newlines normalized here so the same code on Windows and on CI produces the same bytes,
            // and a rebuild elsewhere is not a diff.
            var contents = Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n").TrimEnd() + "\n";

            var full = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            // Only when it changed: rewriting an identical file on every build would invalidate
            // whatever depends on its timestamp and make the build cascade.
            if (File.Exists(full) && await File.ReadAllTextAsync(full) == contents)
            {
                Console.WriteLine($"OpenAPI document unchanged: {full}");
                return;
            }

            await File.WriteAllTextAsync(full, contents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.WriteLine($"OpenAPI document written: {full}");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private sealed class SilentServer : IServer
    {
        public IFeatureCollection Features { get; } = new FeatureCollection();

        public void Dispose() { }

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
            where TContext : notnull => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
