using Elijah.Logic.Abstract;
using MQTTnet;

namespace Elijah.Logic.Concrete;

// ----------------------------------------------------------- //
// MQTT Broker connection management service                   //
// Handles connect/disconnect operations with error handling   //
// ----------------------------------------------------------- //
public class MqttConnectionService(IMqttClient client, MqttClientOptions options)
    : IMqttConnectionService
{
    // --------------------------------------------------------- //
    // Exposes the MQTT client instance for external operations  //
    // --------------------------------------------------------- //
    public IMqttClient Client { get; } = client;
    
    // --------------------------- //
    // Connects to the MQTT broker //
    // --------------------------- //
    public async Task ConnectAsync()
    {
        try
        {
            Console.WriteLine("Connecting to MQTT...");
            await Client.ConnectAsync(options, CancellationToken.None);
            Console.WriteLine(Client.IsConnected
                ? "MQTT connection successful"
                : "MQTT connection failed: Client not connected after attempt");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MQTT connection failed: {ex.Message}");
        }
    }

    // -------------------------------- //
    // Disconnects from the MQTT broker //
    // -------------------------------- //
    public async Task DisconnectAsync()
    {
        if (Client.IsConnected)
        {
            await Client.DisconnectAsync();
        }
    }
}