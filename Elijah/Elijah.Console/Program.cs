using Elijah.Data;
using Elijah.Logic.Abstract;
using Elijah.Logic.Concrete;
using Elijah.Logic.Injection;
using LogManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GenericRepository;

IConfiguration configuration = new ConfigurationBuilder()
             .SetBasePath(Directory.GetCurrentDirectory())
             .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
             .Build();

//Create servicecollection
IServiceCollection services = new ServiceCollection();

//Add lib dependencies to collection
ServiceMapper.ConfigureServices(services, configuration);

//Generate a provider object from all registered services
ServiceProvider serviceProvider = services.BuildServiceProvider();
IZigbeeClient zigbeeClient = serviceProvider.GetService<IZigbeeClient>();
IDeviceTemplateService deviceTemplateService = serviceProvider.GetService<IDeviceTemplateService>();

// await deviceTemplateService.ModelPresentAsync("SNZB-03P", "testaddress");
// await zigbeeClient.ConnectToMqtt();

// Auto-create tables if they don't exist
// var dbContext = serviceProvider.GetService<ApplicationDbContext>();
// await dbContext.Database.EnsureCreatedAsync();
// Console.WriteLine("Database tables ensured created");

await Task.Delay(1000);
await zigbeeClient.subscribeToAll();
await Task.Delay(1000);
// await zigbeeClient.AllowJoinAndListen(15);
// await Task.Delay(1000);
zigbeeClient.StartProcessingMessages();
await Task.Delay(Timeout.Infinite); 
// Task.Delay(1000);
// await zigbeeClient.RemoveDevice("0xa4c138024a75ffff");
// await zigbeeClient.RemoveDevice("0xd44867fffe2a920a");




