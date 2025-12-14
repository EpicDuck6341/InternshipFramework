using Elijah.Logic.Abstract;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Elijah.Logic;

public class MainService(IZigbeeClient client) : IHostedService
{
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await client.ConnectToMqtt();
        await client.SubscribeToAll();              // Subscribe to devices
        // client.StartProcessingMessages();           // Start receiving data
    
        Console.WriteLine("System ready. Telemetry will flow to Azure IoT Hub.");
        // await client.AllowJoinAndListen(15);
        // await Task.Delay(1000);
        // await Task.Delay(1000, cancellationToken);
        // await client.SubscribeToAll();
        // await Task.Delay(1000, cancellationToken);
        // client.StartProcessingMessages();

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

