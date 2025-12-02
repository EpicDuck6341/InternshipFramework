using Elijah.Logic.Abstract;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;

namespace Elijah.Logic.Concrete;

public class SubscriptionService(
    IMqttConnectionService mqtt,
    IServiceScopeFactory scopeFactory,
    IDeviceService device
    ) : ISubscriptionService
{
    public async Task SubscribeExistingAsync()
    {
        
        using var scope = scopeFactory.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        
        var unsubscribedAddresses = await devices.GetUnsubscribedAddressesAsync();

        foreach (var addr in unsubscribedAddresses)
        {
            await mqtt.Client.SubscribeAsync($"zigbee2mqtt/{addr}");
            await devices.SetSubscribedStatusAsync(true, addr);
        }
    }

    public async Task SubscribeAsync(string address)
    {
        using var scope = scopeFactory.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        await mqtt.Client.SubscribeAsync($"zigbee2mqtt/{address}");
        await devices.SetSubscribedStatusAsync(true, address);
    }
    public async Task SubscribeAllActiveDevicesAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        // Get all active addresses
        var addresses = await devices.GetActiveAddressesAsync();
        if (addresses == null)
        {
            Console.WriteLine("No devices");
        }

        foreach (var addr in addresses)
        {
            await SubscribeAsync(addr); // your existing SubscribeAsync function
            Console.WriteLine($"Subscribed to {addr}");
        }
    }

    
    
}