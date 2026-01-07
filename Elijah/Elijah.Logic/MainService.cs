using Elijah.Logic.Abstract;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Elijah.Logic;

public class MainService(IZigbeeClient client) : IHostedService
{
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await client.ConnectToMqtt();
        await client.SubscribeToAll();             
        Console.WriteLine("System ready. Telemetry will flow to Azure IoT Hub.");
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }
    
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await Log.CloseAndFlushAsync();
    }
    
}

