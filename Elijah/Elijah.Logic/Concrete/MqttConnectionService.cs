using Elijah.Logic.Abstract;
using MQTTnet;
using Microsoft.Extensions.Logging;
using FacilicomLogManager.Extensions;

namespace Elijah.Logic.Concrete;

// ----------------------------------------------------------- //
// MQTT Broker connection management service                   //
// Handles connect/disconnect operations with error handling   //
// ----------------------------------------------------------- //
public class MqttConnectionService(
    IMqttClient client, 
    MqttClientOptions options,
    ILogger<MqttConnectionService> logger)
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
        logger
            .WithFacilicomContext(friendlyMessage: $"Verbinden met MQTT broker")
            .SendLogInformation("Start ConnectAsync");

        try
        {
            Console.WriteLine("Connecting to MQTT...");
            await Client.ConnectAsync(options, CancellationToken.None);
            
            if (Client.IsConnected)
            {
                logger
                    .WithFacilicomContext(friendlyMessage: $"MQTT verbinding succesvol")
                    .SendLogInformation("MQTT verbinding succesvol");
            }
            else
            {
                logger
                    .WithFacilicomContext(friendlyMessage: $"MQTT verbinding mislukt")
                    .SendLogError("MQTT connection failed: Client not connected after attempt");
            }
            
            Console.WriteLine(Client.IsConnected
                ? "MQTT connection successful"
                : "MQTT connection failed: Client not connected after attempt");
        }
        catch (Exception ex)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"MQTT verbinding mislukt")
                .SendLogError(ex, "ConnectAsync fout - Message: {Message}", ex.Message);
            Console.WriteLine($"MQTT connection failed: {ex.Message}");
        }
    }

    // -------------------------------- //
    // Disconnects from the MQTT broker //
    // -------------------------------- //
    public async Task DisconnectAsync()
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Verbinding MQTT verbreken")
            .SendLogInformation("DisconnectAsync started");

        if (Client.IsConnected)
        {
            await Client.DisconnectAsync();
            logger
                .WithFacilicomContext(friendlyMessage: $"MQTT verbinding verbroken")
                .SendLogInformation("DisconnectAsync voltooid");
        }
    }
}