using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elijah.Logic.Abstract;
using MQTTnet;

namespace Elijah.Logic.Concrete;

public class ReceiveService(
    IMqttConnectionService mqtt,
    IDeviceService devices,
    IDeviceFilterService filters
) : IReceiveService
{
    public void StartMessageLoop()
    {
        mqtt.Client.ApplicationMessageReceivedAsync += OnMessageAsync;
    }

    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        var topic = arg.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);

        if (topic.Contains("zigbee2mqtt/bridge"))
            return; // handled elsewhere

        var deviceAddress = topic.Replace("zigbee2mqtt/", "");
        var modelId = await devices.QueryModelIdAsync(deviceAddress);
        var keys = await filters.QueryDataFilterAsync(modelId);

        var node = JsonNode.Parse(payload)?.AsObject();
        if (node == null)
            return;

        var filtered = new JsonObject();
        foreach (var k in keys)
            if (node.ContainsKey(k))
                filtered[k] = node[k]!.DeepClone();

        var device = await devices.GetDeviceByAdressAsync(deviceAddress);
        Console.WriteLine($"[{device?.Name},{modelId}]{filtered.ToJsonString()}");
    }
}
