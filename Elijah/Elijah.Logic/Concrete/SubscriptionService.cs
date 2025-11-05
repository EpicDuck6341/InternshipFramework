using Elijah.Logic.Abstract;
using MQTTnet;
using MQTTnet.Protocol;

namespace Elijah.Logic.Concrete;

public class SubscriptionService : ISubscriptionService
{
    private readonly MqttConnectionService _mqtt;
    private readonly IDeviceService _devices;

    public SubscriptionService(MqttConnectionService mqtt, IDeviceService devices)
    {
        _mqtt = mqtt;
        _devices = devices;
    }

    public async Task SubscribeExistingAsync()
    {
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
        await _mqtt.Client.SubscribeAsync($"zigbee2mqtt/{address}");
        await _devices.SetSubscribedStatusAsync(true, address);
    }
    
    
}