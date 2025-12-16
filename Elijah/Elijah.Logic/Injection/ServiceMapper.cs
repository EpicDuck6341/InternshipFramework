using System.IO.Ports;
using Elijah.Data.Context;
using Elijah.Data.Repository;
using Elijah.Domain.Config;
using Elijah.Logic.Abstract;
using Elijah.Logic.Concrete;
using GenericRepository.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet;

namespace Elijah.Logic.Injection;

public static class ServiceMapper
{
    public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // DbContext
        services.AddDbContextPool<ApplicationDbContext>(options =>
            options
                .UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
        );

        // Services
        var portName = configuration["OpenTherm:SerialPort"]
                       ?? throw new InvalidOperationException("OpenTherm:SerialPort not configured");

        var baudRate = int.TryParse(configuration["OpenTherm:BaudRate"], out var br)
            ? br
            : 115200;

        services.AddSingleton(new SerialPort(portName, baudRate)
        {
            ReadTimeout = 2000,
            WriteTimeout = 2000
        });

        // Add these registrations:
        services.AddSingleton<IAzureIoTHubService, AzureIoTHubService>();
        services.AddHostedService<ZigbeeCommandService>();
        
        services.AddSingleton<ReceiveService>();  
        services.AddSingleton<IReceiveService>(sp => sp.GetRequiredService<ReceiveService>());  
        services.AddHostedService(sp => sp.GetRequiredService<ReceiveService>()); 


        // Ensure ModuleClient is available:
        services.AddSingleton(provider => EdgeAdapter.ModuleClient); // Expose the static client

        services.AddSingleton<IMqttConnectionService, MqttConnectionService>();

        services.AddTransient<IDeviceService, DeviceService>();
        services.AddTransient<IDeviceFilterService, DeviceFilterService>();
        services.AddTransient<IConfiguredReportingsService, ConfiguredReportingsService>();
        services.AddTransient<IOptionService, OptionService>();
        services.AddTransient<IDeviceTemplateService, DeviceTemplateService>();

        services.AddTransient<ISubscriptionService, SubscriptionService>();
        services.AddTransient<ISendService, SendService>();
        services.AddSingleton<OpenThermService>();
        services.AddSingleton<IOpenThermService>(sp => sp.GetRequiredService<OpenThermService>());
        services.AddHostedService(sp => sp.GetRequiredService<OpenThermService>());

        // Zigbee client
        services.AddSingleton<IZigbeeClient, ZigbeeClient>();
        //Implement all as Transient bump

        #region MQTT Client

        //Get MQTT Config
        var mqttConfig = configuration.GetSection("MqttConfig").Get<MqttConfig>();

        // Register MqttClientOptions
        services.AddSingleton(_ =>
            new MqttClientOptionsBuilder()
                .WithTcpServer(mqttConfig?.HostName, mqttConfig?.Port)
                .WithClientId(mqttConfig?.ClientId)
                .Build()
        );

        // Register IMqttClient
        services.AddSingleton<IMqttClient>(_ => new MqttClientFactory().CreateMqttClient());

        #endregion

        //Repository
        services.AddGenericRepository<ApplicationDbContext, IZigbeeRepository, ZigbeeRepository>();
    }
}