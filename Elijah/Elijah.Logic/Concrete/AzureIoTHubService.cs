using Microsoft.Azure.Devices.Client;
using System.Text;
using System.Text.Json;
using Elijah.Logic.Abstract;

namespace Elijah.Logic.Concrete;

// ------------------------------------------------------- //
// Azure IoT Hub communication service                     //
// Handles telemetry sending and cloud-to-device commands  //
// ------------------------------------------------------- //
public class AzureIoTHubService(ModuleClient moduleClient) : IAzureIoTHubService
{
    public async Task SendTelemetryAsync(string deviceId, string property, object value)
    {
        var telemetry = new
        {
            device = deviceId,
            property,
            value,
            timestamp = DateTime.UtcNow
        };

        string json = JsonSerializer.Serialize(telemetry);
        var message = new Message(Encoding.UTF8.GetBytes(json))
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8"
        };

        // Add routing properties if needed
        message.Properties.Add("deviceId", deviceId);
        message.Properties.Add("propertyName", property);

        await moduleClient.SendEventAsync("telemetry", message);
        Console.WriteLine($"📤 Sent telemetry: {json}");
    }

    public async Task SendBatchTelemetryAsync(string deviceId, Dictionary<string, object> properties)
    {
        var telemetry = new
        {
            device = deviceId,
            properties,
            timestamp = DateTime.UtcNow
        };

        string json = JsonSerializer.Serialize(telemetry);
        var message = new Message(Encoding.UTF8.GetBytes(json))
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8"
        };

        await moduleClient.SendEventAsync("telemetry", message);
        Console.WriteLine($"Sent batch telemetry for {deviceId}: {properties.Count} properties");
    }
}