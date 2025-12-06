using Elijah.Data.Context;
using Elijah.Logic;
using Elijah.Logic.Abstract;
using Elijah.Logic.Concrete;
using Elijah.Logic.Injection;
using FacilicomLogManager.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

// Build the host first
var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(builder =>
        builder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    )
    .ConfigureServices(
        (context, serviceCollection) =>
        {
            // var logger = new LoggerConfiguration()
                // Set information as the minimal level
                // .MinimumLevel.Information()
                // .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                // .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                // // Add Facilicom-specific enrichment (Enriches logs with application name and exception details)
                // .AddFacilicomEnrichment(context.Configuration)
                // // Configure the console sink for local debugging
                // .WriteTo.FacilicomConsoleSink()
                // // Configure the file sink for persistent local logs
                // .WriteTo.FacilicomFileSink(context.Configuration)
                // // Configure the HTTP sink for sending logs to the logging service
                // .WriteTo.FacilicomHttpSink(context.Configuration)
                // .CreateLogger();

            // Set the configured logger as the global static logger instance
            // Log.Logger = logger;
            // Register Serilog with the dependency injection system
            serviceCollection.AddSerilog();

            ServiceMapper.ConfigureServices(serviceCollection, context.Configuration);
            serviceCollection.AddHostedService<MainService>();
        }
    )
    .Build(); // <-- Build the host here

// Initialize database AFTER building but BEFORE running
using (var scope = host.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var dbContext = services.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync(); // Apply migrations properly
    Console.WriteLine("Database migrated (development mode)");
}


// Run the host
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var edge = await EdgeAdapter.InitialiseAsync(cts.Token);
await host.RunAsync(cts.Token);   // existing code keeps running