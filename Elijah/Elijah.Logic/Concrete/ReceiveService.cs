using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elijah.Logic.Abstract;
using MQTTnet;

namespace Elijah.Logic.Concrete;

public class ReceiveService(IMqttConnectionService _mqtt,
    IDeviceService _devices,
    IDeviceFilterService _filters) : IReceiveService
{
    

    public void StartMessageLoop()
    {
        _mqtt.Client.ApplicationMessageReceivedAsync += OnMessageAsync;
    }

    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        var topic = arg.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);

        if (topic.Contains("zigbee2mqtt/bridge")) return; // handled elsewhere

        var deviceAddress = topic.Replace("zigbee2mqtt/", "");
        var modelId = await _devices.QueryDeviceNameAsync(deviceAddress);
        var keys    = await _filters.QueryDataFilterAsync(modelId);

        var node = JsonNode.Parse(payload)?.AsObject();
        if (node == null) return;

        var filtered = new JsonObject();
        foreach (var k in keys)
            if (node.ContainsKey(k))
                filtered[k] = node[k]!.DeepClone();

        Console.WriteLine(
            $"[{await _devices.QueryDeviceNameAsync(modelId)},{modelId}]{filtered.ToJsonString()}");
    }
}