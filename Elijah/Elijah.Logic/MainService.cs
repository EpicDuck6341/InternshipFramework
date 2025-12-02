using Elijah.Data.Context;
using Elijah.Logic.Abstract;
using Elijah.Logic.Concrete;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Elijah.Logic;

public class MainService(IZigbeeClient client, IDeviceTemplateService deviceTemplateService) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await client.ConnectToMqtt();
        
        // await client.AllowJoinAndListen(15);
        // await Task.Delay(1000);
        await Task.Delay(1000);
        client.subscribeToAll();
        await Task.Delay(1000);
        client.StartProcessingMessages();

        // Task.Delay(1000);
        // await client.RemoveDevice("0xa4c138024a75ffff");
        // await client.RemoveDevice("0xd44867fffe2a920a");

        // Keep service alive
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Log.CloseAndFlushAsync();
    }
}

