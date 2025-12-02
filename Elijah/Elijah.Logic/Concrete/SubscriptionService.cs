using Elijah.Logic.Abstract;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;

namespace Elijah.Logic.Concrete;

public class SubscriptionService(
    IMqttConnectionService mqtt,
    IServiceScopeFactory scopeFactory
) : ISubscriptionService
{
    public async Task SubscribeExistingAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        var unsubscribedAddresses = await deviceService.GetUnsubscribedAddressesAsync();

        foreach (var addr in unsubscribedAddresses)
        {
            await mqtt.Client.SubscribeAsync($"zigbee2mqtt/{addr}");
            var device = await deviceService.GetDeviceByAdressAsync(addr);
            if (device != null) device.Subscribed = true;
        }
    }

    public async Task SubscribeAsync(string address)
    {
        using var scope = scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        await mqtt.Client.SubscribeAsync($"zigbee2mqtt/{address}");
        var device = await deviceService.GetDeviceByAdressAsync(address);
        if (device != null) device.Subscribed = true;
    }

    public async Task SubscribeAllActiveDevicesAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        // Get all active addresses
        var addresses = await devices.GetActiveAddressesAsync();
        foreach (var addr in addresses)
        {
            await SubscribeAsync(addr); // your existing SubscribeAsync function
            Console.WriteLine($"Subscribed to {addr}");
        }
    }
}