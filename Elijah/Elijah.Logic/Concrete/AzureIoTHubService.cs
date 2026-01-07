using Microsoft.Azure.Devices.Client;
using System.Text;
using System.Text.Json;
using Elijah.Logic.Abstract;
using Microsoft.Extensions.Logging;
using FacilicomLogManager.Extensions;

namespace Elijah.Logic.Concrete;

// ------------------------------------------------------- //
// Azure IoT Hub communication service                     //
// Handles telemetry sending and cloud-to-device commands  //
// ------------------------------------------------------- //
public class AzureIoTHubService(
    ModuleClient moduleClient,
    ILogger<AzureIoTHubService> logger) : IAzureIoTHubService
{
    public async Task SendTelemetryAsync(string deviceId, string property, object value)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Verzenden telemetrie voor device {deviceId}")
            .SendLogInformation("Start telemetrie verzending - Device: {DeviceId}, Property: {Property}, Value: {Value}", deviceId, property, value);

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

        message.Properties.Add("deviceId", deviceId);
        message.Properties.Add("propertyName", property);

        await moduleClient.SendEventAsync("telemetry", message);

        logger
            .WithFacilicomContext(friendlyMessage: $"Telemetrie succesvol verzonden voor {deviceId}")
            .SendLogInformation("Finished telemetrie verzonden");
    }

    public async Task SendBatchTelemetryAsync(string deviceId, Dictionary<string, object> properties)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Batch telemetrie voor device {deviceId}")
            .SendLogInformation("Verzenden batch telemetrie - Device: {DeviceId}, Aantal properties: {Count}", deviceId, properties.Count);

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
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Batch telemetrie verzonden voor {deviceId}")
            .SendLogInformation("Batch telemetrie verzonden voor {DeviceId}: {Count} properties", deviceId, properties.Count);
    }
}