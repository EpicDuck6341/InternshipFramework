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
        services.AddSingleton(new SerialPort("/dev/ttyUSB1", 115200)
        {
            ReadTimeout = 2000,
            WriteTimeout = 2000
        });

        // Add these registrations:
        services.AddSingleton<IAzureIoTHubService, AzureIoTHubService>();
        services.AddHostedService<ZigbeeCommandService>();


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
        services.AddTransient<IReceiveService, ReceiveService>();
        services.AddHostedService<OpenThermService>();
        services.AddSingleton<IOpenThermService>(sp =>
            sp.GetServices<IHostedService>().OfType<OpenThermService>().First());

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