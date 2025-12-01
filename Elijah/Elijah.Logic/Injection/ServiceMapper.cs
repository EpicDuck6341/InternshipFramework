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
            services.AddSingleton(configuration);

            // Services
            //As singleton, since we only make on connection
            services.AddSingleton<IMqttConnectionService, MqttConnectionService>();

            services.AddTransient<IDeviceService, DeviceService>();
            services.AddTransient<IDeviceFilterService, DeviceFilterService>();
            services.AddTransient<IConfiguredReportingsService, ConfiguredReportingsService>();
            services.AddTransient<IOptionService, OptionService>();
            services.AddTransient<IDeviceTemplateService, DeviceTemplateService>();

            services.AddTransient<ISubscriptionService, SubscriptionService>();
            services.AddTransient<ISendService, SendService>();
            services.AddTransient<IReceiveService, ReceiveService>();
            services.AddTransient<IOpenThermService, OpenThermService>();
            
            // Zigbee client
            services.AddSingleton<IZigbeeClient, ZigbeeClient>();
            //Implement all as Transient bump


            //Repository
            services.AddGenericRepository<ApplicationDbContext, IZigbeeRepository, ZigbeeRepository>();

            // Batch settings
            //services.AddSingleton(configuration.GetSection("BatchSettings").Get<BatchSettings>());
        }
    }
}