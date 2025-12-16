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
    IServiceScopeFactory scopeFactory,
    IDeviceService deviceService
) : ISubscriptionService
{
  

    // ------------------------------------ //
    // Subscribes to a single device topic  //
    // ------------------------------------ //
    public async Task SubscribeAsync(string address)
    {
        await mqtt.Client.SubscribeAsync($"zigbee2mqtt/{address}");
        await deviceService.SetSubscribedAsync(address);
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
    
    public async Task<bool> IsSubscribedAsync(string address)
    {
        using var scope = scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var device = await deviceService.GetDeviceByAddressAsync(address);
        if (device != null && device.Subscribed)
        {
            return true;
        }
        return false;
    }
}