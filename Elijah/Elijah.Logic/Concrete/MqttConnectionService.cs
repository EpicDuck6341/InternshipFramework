using Elijah.Logic.Abstract;
using MQTTnet;
using Microsoft.Extensions.Configuration;

namespace Elijah.Logic.Concrete;

public class MqttConnectionService : IMqttConnectionService
{
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;

    public IMqttClient Client => _client;

    public MqttConnectionService(IConfiguration cfg)
    {
        var section = cfg.GetSection("MQTTString");
        _options = new MqttClientOptionsBuilder()
            .WithTcpServer(section["Hostname"], int.Parse(section["Port"]))
            .WithClientId(section["ClientId"])
            .Build();

        _client = new MqttClientFactory().CreateMqttClient();
    }

    public async Task ConnectAsync()
    {
        try
        {
            Console.WriteLine("Connecting to MQTT...");
            await _client.ConnectAsync(_options, CancellationToken.None);
        
            if (_client.IsConnected)
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
        await _client.DisconnectAsync();
    }
}