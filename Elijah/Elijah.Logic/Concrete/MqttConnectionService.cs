using Elijah.Logic.Abstract;
using MQTTnet;

namespace Elijah.Logic.Concrete;

public class MqttConnectionService(IMqttClient client, MqttClientOptions options)
    : IMqttConnectionService
{
    public IMqttClient Client { get; }

    public async Task ConnectAsync()
    {
        if (!client.IsConnected)
        {
            await client.ConnectAsync(options, CancellationToken.None);
        }
    }

    public async Task DisconnectAsync()
    {
        if (client.IsConnected)
        {
            await client.DisconnectAsync();
        }
    }
}
