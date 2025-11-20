using Elijah.Logic.Abstract;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Protocol;

namespace Elijah.Logic.Concrete;

public class SubscriptionService(IMqttConnectionService _mqtt,IServiceScopeFactory _scopeFactory) : ISubscriptionService
{
  

    public async Task SubscribeExistingAsync()
    {
        
        using var scope = _scopeFactory.CreateScope();
        var _devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        
        var unsubbed = await _devices.GetUnsubscribedAddressesAsync();
        if (unsubbed == null) return;

        foreach (var addr in unsubbed)
        {
            await _mqtt.Client.SubscribeAsync($"zigbee2mqtt/{addr}");
            await _devices.SetSubscribedStatusAsync(true, addr);
        }
    }

    public async Task SubscribeAsync(string address)
    {
        using var scope = _scopeFactory.CreateScope();
        var _devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        await _mqtt.Client.SubscribeAsync($"zigbee2mqtt/{address}");
        await _devices.SetSubscribedStatusAsync(true, address);
    }
    
    
}