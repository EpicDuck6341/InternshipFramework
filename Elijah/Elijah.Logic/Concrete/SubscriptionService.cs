using Elijah.Logic.Abstract;
using MQTTnet;
using MQTTnet.Protocol;

namespace Elijah.Logic.Concrete;

public class SubscriptionService(IMqttConnectionService mqtt, IDeviceService devices)
    : ISubscriptionService
{
    public async Task SubscribeExistingAsync()
    {
        var unsubscribedAddresses = await devices.GetUnsubscribedAddressesAsync();

        foreach (var addr in unsubscribedAddresses)
        {
            await mqtt.Client.SubscribeAsync($"zigbee2mqtt/{addr}");
            await devices.SetSubscribedStatusAsync(true, addr);
        }
    }

    public async Task SubscribeAsync(string address)
    {
        await mqtt.Client.SubscribeAsync($"zigbee2mqtt/{address}");
        await devices.SetSubscribedStatusAsync(true, address);
    }
}
