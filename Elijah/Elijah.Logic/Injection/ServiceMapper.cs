using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain;
using Elijah.Logic.Abstract;
using Elijah.Logic.Concrete;
using GenericRepository.Utilities;
using LogManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using GenericRepository.Model;

namespace Elijah.Logic.Injection
{
    public class ServiceMapper
    {
        public static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // DbContext
            services.AddDbContextPool<ApplicationDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                    .EnableSensitiveDataLogging()
                    .EnableDetailedErrors());

            // Logging
            // services.InitialiseFsLogging(configuration);

            // Services
            services.AddSingleton<MqttConnectionService>();
            services.AddSingleton<IMqttConnectionService, MqttConnectionService>(); // if you use the interface

// 2. Domain services (the ones you already have)
            services.AddScoped<IDeviceService, DeviceService>();
            services.AddScoped<IDeviceFilterService, DeviceFilterService>();
            services.AddScoped<IConfiguredReportingsService, ConfiguredReportingsService>();
            services.AddScoped<IOptionService, OptionService>();
            services.AddScoped<IDeviceTemplateService, DeviceTemplateService>();

// 3. MQTT helper services
            services.AddScoped<ISubscriptionService, SubscriptionService>();
            services.AddScoped<ISendService, SendService>();
            services.AddScoped<IReceiveService, ReceiveService>();

// 4. Façade (depends on all above)
            services.AddScoped<IZigbeeClient, ZigbeeClient>();


            // Zigbee client
            services.AddSingleton<IZigbeeClient, ZigbeeClient>();


            //Repository
            services.AddGenericRepository<ApplicationDbContext, IExampleRepository, ExampleRepository>();

            // Batch settings
            //services.AddSingleton(configuration.GetSection("BatchSettings").Get<BatchSettings>());
        }
    }
}