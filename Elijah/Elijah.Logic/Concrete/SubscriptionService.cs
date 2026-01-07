using Elijah.Logic.Abstract;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using Microsoft.Extensions.Logging;
using FacilicomLogManager.Extensions;

namespace Elijah.Logic.Concrete;

// ----------------------------------------------- //
// MQTT topic subscription management service      //
// Controls device-specific message subscriptions  //
// ----------------------------------------------- //
public class SubscriptionService(
    IMqttConnectionService mqtt,
    IServiceScopeFactory scopeFactory,
    IDeviceService deviceService,
    ILogger<SubscriptionService> logger) : ISubscriptionService
{
  

    // ------------------------------------ //
    // Subscribes to a single device topic  //
    // ------------------------------------ //
    public async Task SubscribeAsync(string address)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Subscriben naar device {address}")
            .SendLogInformation("SubscribeAsync - Address: {Address}", address);

        await mqtt.Client.SubscribeAsync($"zigbee2mqtt/{address}");
        await deviceService.SetSubscribedAsync(address);
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Geabonneerd op {address}")
            .SendLogInformation("SubscribeAsync voltooid");
    }

    // ---------------------------------------------------- //
    // Subscribes to all devices marked active in database  //
    // ---------------------------------------------------- //
    public async Task SubscribeAllActiveDevicesAsync()
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Subscriben naar alle actieve devices")
            .SendLogInformation("SubscribeAllActiveDevicesAsync started");

        using var scope = scopeFactory.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        
        var addresses = await devices.GetActiveAddressesAsync();
        foreach (var addr in addresses)
        {
            await SubscribeAsync(addr); 
            logger
                .WithFacilicomContext(friendlyMessage: $"Geabonneerd op {addr}")
                .SendLogInformation("Subscribed to {Address}", addr);
        }
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Alle devices geabonneerd")
            .SendLogInformation("SubscribeAllActiveDevicesAsync voltooid - Count: {Count}", addresses.Count);
    }
    
    public async Task<bool> IsSubscribedAsync(string address)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Controleren subscription voor {address}")
            .SendLogInformation("IsSubscribedAsync - Address: {Address}", address);

        using var scope = scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var device = await deviceService.GetDeviceByAddressAsync(address);
        if (device != null && device.Subscribed)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Device is geabonneerd")
                .SendLogInformation("Device is subscribed");
            return true;
        }
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Device is niet geabonneerd")
            .SendLogInformation("Device is not subscribed");
        return false;
    }
}