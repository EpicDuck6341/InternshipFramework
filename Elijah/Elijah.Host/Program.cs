using Elijah.Logic;
using Elijah.Logic.Injection;
using FacilicomLogManager.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

await Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(builder =>
        builder
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    )
    .ConfigureServices(
        (context, serviceCollection) =>
        {
            var logger = new LoggerConfiguration()
                // Set information as the minimal level
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                // Add Facilicom-specific enrichment (Enriches logs with application name and exception details)
                .AddFacilicomEnrichment(context.Configuration)
                // Configure the console sink for local debugging
                .WriteTo.FacilicomConsoleSink()
                // Configure the file sink for persistent local logs
                .WriteTo.FacilicomFileSink(context.Configuration)
                // Configure the HTTP sink for sending logs to the logging service
                .WriteTo.FacilicomHttpSink(context.Configuration)
                .CreateLogger();

            // Set the configured logger as the global static logger instance
            Log.Logger = logger;
            // Register Serilog with the dependency injection system
            serviceCollection.AddSerilog();

            ServiceMapper.ConfigureServices(serviceCollection, context.Configuration);
            serviceCollection.AddHostedService<MainService>();
        }
    )
    .Build()
    .RunAsync();
