using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using System.Text.Json;

public sealed class EdgeAdapter
{
    private static ModuleClient? _ioTClient;

    public static async Task<ModuleClient?> InitialiseAsync(CancellationToken ct)
    {
        var transport = new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) };

        if (Environment.GetEnvironmentVariable("IOTEDGE_WORKLOADURI") != null)
        {
            // Running as IoT Edge module
            _ioTClient = await ModuleClient.CreateFromEnvironmentAsync(transport);
            await _ioTClient.OpenAsync(ct);
            await _ioTClient.SetMethodDefaultHandlerAsync(DefaultMethodHandler, null, ct);

            // Write twin desired properties to disk
            var twin = await _ioTClient.GetTwinAsync(ct);
            var twinPath = Path.Combine(AppContext.BaseDirectory, "config", "twin.json");
            Directory.CreateDirectory(Path.GetDirectoryName(twinPath)!);
            await File.WriteAllTextAsync(twinPath, JsonSerializer.Serialize(twin.Properties.Desired), ct);

            // Re-load whenever twin changes
            await _ioTClient.SetDesiredPropertyUpdateCallbackAsync(OnDesired, null, ct);
        }
        else
        {
            // Local development mode
            Console.WriteLine("IOTEDGE_WORKLOADURI not found; running in local development mode.");
            var twinPath = Path.Combine(AppContext.BaseDirectory, "config", "twin.json");
            Directory.CreateDirectory(Path.GetDirectoryName(twinPath)!);
            if (!File.Exists(twinPath))
                await File.WriteAllTextAsync(twinPath, "{}"); // create empty twin file
        }

        return _ioTClient;
    }

    private static Task<MethodResponse> DefaultMethodHandler(MethodRequest req, object ctx)
        => Task.FromResult(new MethodResponse(200)); // 200 = OK

    private static async Task OnDesired(TwinCollection desired, object ctx)
    {
        var twinPath = Path.Combine(AppContext.BaseDirectory, "config", "twin.json");
        Directory.CreateDirectory(Path.GetDirectoryName(twinPath)!);
        await File.WriteAllTextAsync(twinPath, desired.ToJson());
    }
}
