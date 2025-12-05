using Elijah.Logic.Abstract;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;

namespace Elijah.Logic.Concrete;

// ----------------------------------------------- //
// MQTT topic subscription management service      //
// Controls device-specific message subscriptions  //
// ----------------------------------------------- //
public class SubscriptionService(
    IMqttConnectionService mqtt,
    IServiceScopeFactory scopeFactory
) : ISubscriptionService
{
  

    // ------------------------------------ //
    // Subscribes to a single device topic  //
    // ------------------------------------ //
    public async Task SubscribeAsync(string address)
    {
        using var scope = scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        await mqtt.Client.SubscribeAsync($"zigbee2mqtt/{address}");
        var device = await deviceService.GetDeviceByAddressAsync(address);
        if (device != null) device.Subscribed = true;
    }

    // ---------------------------------------------------- //
    // Subscribes to all devices marked active in database  //
    // ---------------------------------------------------- //
    public async Task SubscribeAllActiveDevicesAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        
        var addresses = await devices.GetActiveAddressesAsync();
        foreach (var addr in addresses)
        {
            await SubscribeAsync(addr); 
            Console.WriteLine($"Subscribed to {addr}");
        }
    }
}