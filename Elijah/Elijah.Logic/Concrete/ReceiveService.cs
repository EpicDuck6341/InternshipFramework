using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elijah.Domain.Entities;
using Elijah.Domain.Models;
using Elijah.Logic.Abstract;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Protocol;

namespace Elijah.Logic.Concrete;

public class ReceiveService(
    IServiceScopeFactory scopeFactory,
    IMqttConnectionService mqtt
    ) : IReceiveService
{
    private bool _subscribed = false;
    //List for all addresses which timed out on the receive option method
    private readonly Dictionary<string, PendingOptionData> _lateOptions 
        = new Dictionary<string, PendingOptionData>();

    public void StartMessageLoop()
    {
        mqtt.Client.ApplicationMessageReceivedAsync += OnMessageAsync;
    }

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
        var modelId = await devices.QueryModelIdAsync(deviceAddress);
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
                // filtered[kv.Key] = kv.Value!.DeepClone();
                await filters.NewFilterEntryAsync(deviceAddress, kv.Key);
            }
        }
        Console.WriteLine(
            $"[{await devices.QueryDeviceNameAsync(deviceAddress)},{modelId}]{filtered.ToJsonString()}");

        
        if (_lateOptions.TryGetValue(deviceAddress, out var pending))
        {
            Console.WriteLine($"Late options received for {deviceAddress}");

            await LateOptionAsync(
                payload,
                pending.Address,
                pending.Model,
                pending.ReadableProps,
                pending.Descriptions
            );

            // Remove once handled
            _lateOptions.Remove(deviceAddress);
        }

        
        
    }

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

    /// <summary>
    /// Handles zero sensor values by sending reporting config values sequentially
    /// </summary>
    private async Task HandleZeroSensorValuesAsync(string deviceAddress)
    {
        using var scope = scopeFactory.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguredReportingsService>();
        
        Console.WriteLine($"ALL ZERO SENSOR VALUES detected for device {deviceAddress} - reconfiguring...");
        
        var configs = await config.QueryReportIntervalAsync(deviceAddress);
        if (!configs.Any())
        {
            Console.WriteLine($"No reporting configs found for device {deviceAddress}");
            return;
        }

     
        var baseConfig = configs.First();
        int minInterval = int.Parse(baseConfig.minimum_report_interval ?? "0");
        int maxInterval = int.Parse(baseConfig.maximum_report_interval ?? "0");
        
        int tempChange = int.Parse(configs.FirstOrDefault(c => IsAttributeMatch(c, "temperature"))?.reportable_change ?? "0");
        int humidityChange = int.Parse(configs.FirstOrDefault(c => IsAttributeMatch(c, "humidity"))?.reportable_change ?? "0");
        int co2Change = int.Parse(configs.FirstOrDefault(c => IsAttributeMatch(c, "co2"))?.reportable_change ?? "0");

        
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

    /// <summary>
    /// Sends a single configuration value to ESP via MQTT using proper JSON structure
    /// </summary>
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

    private bool IsAttributeMatch(ReportConfig config, string attributeName)
    {
        return config.attribute.Equals(attributeName, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LateOptionAsync(
        string payload,
        string address,
        string model,
        List<string> readableProps,
        List<string> descriptions)
    {
        using var scope = scopeFactory.CreateScope();
        var option = scope.ServiceProvider.GetRequiredService<IOptionService>();
        var configuredReportings = scope.ServiceProvider.GetRequiredService<IConfiguredReportingsService>();

        var node = JsonNode.Parse(payload);
        if (node == null) return;

        for (int i = 0; i < readableProps.Count; i++)
        {
            var prop = readableProps[i];
            var value = node[prop]?.ToJsonString() ?? "-";
            await option.SetOptionsAsync(address, descriptions[i], value, prop);
            Console.WriteLine($"(LATE) Option: {prop} = {value}");
        }

        var config = await configuredReportings.ConfigByAddress(address);
        await mqtt.Client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic($"zigbee2mqtt/{address}/get")
                .WithPayload("{}")
                .Build()
        );
    }

    
    public void RegisterLateOption(string address, string model,
        List<string> props, List<string> descs)
    {
        _lateOptions[address] = new PendingOptionData
        {
            Address = address,
            Model = model,
            ReadableProps = props,
            Descriptions = descs
        };

        Console.WriteLine($"Stored late options for {address}");
    }

}