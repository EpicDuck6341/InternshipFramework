namespace Elijah.Logic.Abstract;

public interface IAzureIoTHubService
{
    Task SendTelemetryAsync(string deviceId, string property, object value);
    Task SendBatchTelemetryAsync(string deviceId, Dictionary<string, object> properties);
}