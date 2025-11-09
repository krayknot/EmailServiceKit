using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddEmailServiceKit(ctx.Configuration);
        // Add your app services that will enqueue emails, etc.
    });

var host = builder.Build();
await host.RunAsync();
