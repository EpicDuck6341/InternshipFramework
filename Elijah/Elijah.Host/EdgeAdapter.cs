// EdgeAdapter.cs
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using System.Text.Json;

public sealed class EdgeAdapter
{
    private static ModuleClient? _ioTClient;
    public static async Task<ModuleClient> InitialiseAsync(CancellationToken ct)
    {
        var transport = new ITransportSettings[] { new MqttTransportSettings(TransportType.Mqtt_Tcp_Only) };
        _ioTClient = await ModuleClient.CreateFromEnvironmentAsync(transport);
        await _ioTClient.OpenAsync(ct);
        await _ioTClient.SetMethodDefaultHandlerAsync(DefaultMethodHandler, null, ct);

        // write twin desired to disk so the legacy config loader sees it
        var twin = await _ioTClient.GetTwinAsync(ct);
        var twinPath = Path.Combine(AppContext.BaseDirectory, "config", "twin.json");
        Directory.CreateDirectory(Path.GetDirectoryName(twinPath)!);
        await File.WriteAllTextAsync(twinPath, JsonSerializer.Serialize(twin.Properties.Desired), ct);

        // re-load whenever twin changes
        await _ioTClient.SetDesiredPropertyUpdateCallbackAsync(OnDesired, null, ct);

        return _ioTClient;
    }

    private static Task<MethodResponse> DefaultMethodHandler(MethodRequest req, object ctx)
        => Task.FromResult(new MethodResponse(200)); // 200 = OK

    private static async Task OnDesired(TwinCollection desired, object ctx)
    {
        var twinPath = Path.Combine(AppContext.BaseDirectory, "config", "twin.json");
        await File.WriteAllTextAsync(twinPath, desired.ToJson());
        // you can raise a token that forces the host to restart
    }
}