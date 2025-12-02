using Elijah.Logic.Abstract;
using MQTTnet;

namespace Elijah.Logic.Concrete;

public class MqttConnectionService(IMqttClient client, MqttClientOptions options)
    : IMqttConnectionService
{
    public IMqttClient Client { get; } = client;

    public async Task ConnectAsync()
    {
        try
        {
            Console.WriteLine("Connecting to MQTT...");
            await client.ConnectAsync(options, CancellationToken.None);
        
            if (client.IsConnected)
            {
                Console.WriteLine("MQTT connection successful");
            }
            else
            {
                Console.WriteLine("MQTT connection failed: Client not connected after attempt");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($" MQTT connection failed: {ex.Message}");
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
