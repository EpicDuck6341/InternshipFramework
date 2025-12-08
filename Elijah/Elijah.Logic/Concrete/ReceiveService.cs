using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elijah.Domain.Models;
using Elijah.Logic.Abstract;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Protocol;

namespace Elijah.Logic.Concrete;

// --------------------------------------------------------- //
// MQTT Message reception and processing service             //
// Handles incoming device data and option synchronization   //
// --------------------------------------------------------- //
public class ReceiveService(
    IServiceScopeFactory scopeFactory,
    IMqttConnectionService mqtt,
    IAzureIoTHubService azureService
    ) : IReceiveService
{
    
    // ------------------------------------------------------- //
    // Tracks devices that timed out during option retrieval   //
    // ------------------------------------------------------- //
    private readonly Dictionary<string, PendingOptionData> _lateOptions 
        = new Dictionary<string, PendingOptionData>();

    // ---------------------------------------- //
    // Starts the main message processing loop  //
    // ---------------------------------------- //
    public void StartMessageLoop()
    {
        mqtt.Client.ApplicationMessageReceivedAsync += OnMessageAsync;
    }
    // ------------------------------------------ //
    // Main message handler for all MQTT messages //
    // ------------------------------------------ //
    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        
        using var scope = scopeFactory.CreateScope();
        var devices = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var filters = scope.ServiceProvider.GetRequiredService<IDeviceFilterService>();
        var topic = arg.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
        if (topic.Contains("zigbee2mqtt/bridge"))
            return; 
        
        var deviceAddress = topic.Replace("zigbee2mqtt/", "");
        var device = await devices.GetDeviceByAddressAsync(deviceAddress);
        var modelId = device?.DeviceTemplate.ModelId;
        var keys = await filters.QueryDataFilterAsync(deviceAddress);
        var node = JsonNode.Parse(payload)?.AsObject();
        if (node == null) 
            return;
        if (node.ContainsKey("temperature") && node.ContainsKey("co2") && node.ContainsKey("humidity"))
        {
            var tempNode = node["temperature"];
            var co2Node = node["co2"];
            var humidityNode = node["humidity"];
            
            if (IsZeroValue(tempNode) && IsZeroValue(co2Node) && IsZeroValue(humidityNode))
            {
                await HandleZeroSensorValuesAsync(deviceAddress);
            }
        }
        var filtered = new JsonObject();
        if (keys != null && keys.Any())
        {
            foreach (var k in keys)
                if (node.ContainsKey(k))
                    filtered[k] = node[k]!.DeepClone();
        }
        else
        {
            foreach (var kv in node)
            {
                Console.WriteLine(node.Count);
                Console.WriteLine(kv.Key);
                await filters.NewFilterEntryAsync(deviceAddress, kv.Key);
            }
        }
        Console.WriteLine(
            $"[{device?.Name},{modelId}]{filtered.ToJsonString()}");
        foreach (var ft in filtered)
        {
            await azureService.SendTelemetryAsync(deviceAddress, ft.Key, ft.Value);
        }


        if (_lateOptions.TryGetValue(deviceAddress, out var pending))
        {
            Console.WriteLine($"Late options received for {deviceAddress}");

            await LateOptionAsync(
                payload,
                pending.Address,
                pending.ReadableProps,
                pending.Descriptions
            );
            _lateOptions.Remove(deviceAddress);
        }

        
        
    }

    // ---------------------------------------------- //
    // Checks if a JSON node represents a zero value  //
    // ---------------------------------------------- //
    private bool IsZeroValue(JsonNode? node)
    {
        if (node == null) return false;
        
        if (node is JsonValue value)
        {
            if (value.TryGetValue<float>(out var f)) return Math.Abs(f) < float.Epsilon;
            if (value.TryGetValue<int>(out var i)) return i == 0;
            if (value.TryGetValue<double>(out var d)) return Math.Abs(d) < double.Epsilon;
            
            if (value.TryGetValue<string>(out var s))
            {
                if (float.TryParse(s, out var parsedFloat)) return Math.Abs(parsedFloat) < float.Epsilon;
                if (int.TryParse(s, out var parsedInt)) return parsedInt == 0;
            }
        }
        
        return false;
    }


    // --------------------------------------------------------------------------- //
    // Handles zero sensor values by sending reporting config values sequentially  //
    // --------------------------------------------------------------------------- //
    private async Task HandleZeroSensorValuesAsync(string deviceAddress)
    {
        using var scope = scopeFactory.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguredReportingsService>();
        
        Console.WriteLine($"ALL ZERO SENSOR VALUES detected for device {deviceAddress} - reconfiguring...");
        
        var configs = await config.ConfigByAddress(deviceAddress);
        if (!configs.Any())
        {
            Console.WriteLine($"No reporting configs found for device {deviceAddress}");
            return;
        }

     
        var baseConfig = configs.First();
        int minInterval = int.Parse(baseConfig.MinimumReportInterval ?? "0");
        int maxInterval = int.Parse(baseConfig.MaximumReportInterval ?? "0");
        
        int tempChange = int.Parse(configs.FirstOrDefault(c => IsAttributeMatch(c, "temperature"))?.ReportableChange ?? "0");
        int humidityChange = int.Parse(configs.FirstOrDefault(c => IsAttributeMatch(c, "humidity"))?.ReportableChange ?? "0");
        int co2Change = int.Parse(configs.FirstOrDefault(c => IsAttributeMatch(c, "co2"))?.ReportableChange ?? "0");

        
        await SendConfigValueAsync(deviceAddress, "minimumReportInterval", minInterval);
        await Task.Delay(200);
        
        await SendConfigValueAsync(deviceAddress, "maximumReportInterval", maxInterval);
        await Task.Delay(200);
        
        await SendConfigValueAsync(deviceAddress, "temperatureReportableChange", tempChange);
        await Task.Delay(200);
        
        await SendConfigValueAsync(deviceAddress, "humidityReportableChange", humidityChange);
        await Task.Delay(200);
        
        await SendConfigValueAsync(deviceAddress, "co2ReportableChange", co2Change);
        
        Console.WriteLine($"Completed reconfiguration for {deviceAddress}");
    }
    
    // ------------------------------------------------------------------------------- //
    // Sends a single configuration value to ESP via MQTT using proper JSON structure  //
    // ------------------------------------------------------------------------------- //
    private async Task SendConfigValueAsync(string address, string parameterName, int value)
    {
        var payload = new
        {
            brightness = value
        };

        // Convert the payload to a JSON string
        string payloadToSend = JsonSerializer.Serialize(payload);

        // Construct the MQTT message
        var message = new MqttApplicationMessageBuilder()
            .WithTopic($"zigbee2mqtt/{address}/set") 
            .WithPayload(payloadToSend)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await mqtt.Client.PublishAsync(message);
        Console.WriteLine($"Sent config: {address} -> {parameterName}={value}");
    }

    // --------------------------------------------------- //
    // Checks if a config matches the specified attribute  //
    // --------------------------------------------------- //
    private bool IsAttributeMatch(ReportConfig config, string attributeName)
    {
        return config.Attribute != null && config.Attribute.Equals(attributeName, StringComparison.OrdinalIgnoreCase);
    }
    
    
    // ------------------------------------------------- //
    // Processes option data that arrived after timeout  //
    // ------------------------------------------------- //
    private async Task LateOptionAsync(
        string payload,
        string address,
        List<string> readableProps,
        List<string> descriptions)
    {
        using var scope = scopeFactory.CreateScope();
        var option = scope.ServiceProvider.GetRequiredService<IOptionService>();

        var node = JsonNode.Parse(payload);
        if (node == null) return;

        for (int i = 0; i < readableProps.Count; i++)
        {
            var prop = readableProps[i];
            var value = node[prop]?.ToJsonString() ?? "-";
            await option.SetOptionsAsync(address, descriptions[i], value, prop);
            Console.WriteLine($"(LATE) Option: {prop} = {value}");
        }
        
        await mqtt.Client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic($"zigbee2mqtt/{address}/get")
                .WithPayload("{}")
                .Build()
        );
    }

    // ---------------------------------------------- //
    // Registers a device for late option processing  //
    // ---------------------------------------------- //
    public void RegisterLateOption(string address, string model,
        List<string> props, List<string> descriptions)
    {
        _lateOptions[address] = new PendingOptionData
        {
            Address = address,
            Model = model,
            ReadableProps = props,
            Descriptions = descriptions
        };

        Console.WriteLine($"Stored late options for {address}");
    }

}