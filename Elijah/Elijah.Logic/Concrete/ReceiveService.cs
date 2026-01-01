using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elijah.Domain.Models;
using Elijah.Logic.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
    IAzureIoTHubService azureService,
    ISubscriptionService sub,
    IOpenThermService openTherm
) : IReceiveService, IHostedService
{
    private bool _isRunning = false;
    private readonly object _lock = new object();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                Console.WriteLine("ReceiveService already running");
                return Task.CompletedTask;
            }

            _isRunning = true;
        }

        Console.WriteLine("ReceiveService starting automatically...");
        StartMessageLoop(); 
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken)
    {
        mqtt.Client.ApplicationMessageReceivedAsync -= OnMessageAsync;
        Console.WriteLine("ReceiveService stopped");
        return Task.CompletedTask;
    }
    
    private async void StartMessageLoop()
    {
        int attempts = 0;
        while (!mqtt.Client.IsConnected && attempts < 30)
        {
            Console.WriteLine($"Waiting for MQTT connection... ({attempts + 1}/30)");
            await Task.Delay(1000);
            attempts++;
        }

        if (!mqtt.Client.IsConnected)
        {
            Console.WriteLine(" MQTT not connected after 30 seconds!");
            return;
        }

        Console.WriteLine("MQTT connected - starting message processing");
        mqtt.Client.ApplicationMessageReceivedAsync += OnMessageAsync;

        // Subscribe to device topics
        sub.SubscribeAllActiveDevicesAsync();

        Console.WriteLine("Message processing is ACTIVE");
    }

    // ------------------------------------------------------- //
    // Tracks devices that timed out during option retrieval   //
    // ------------------------------------------------------- //
    private readonly Dictionary<string, PendingOptionData> _lateOptions
        = new Dictionary<string, PendingOptionData>();

    // ---------------------------------------- //https://github.com/EpicDuck6341/InternshipFramework/tree/main/Elijah/Elijah.Logic/Concrete
    // Starts the main message processing loop  //
    // ---------------------------------------- //
    // public void StartMessageLoop()
    // {
    //     Console.WriteLine("Message loop started");
    //     mqtt.Client.ApplicationMessageReceivedAsync += OnMessageAsync;
    // }
    // ------------------------------------------ //
    // Main message handler for all MQTT messages //
    // ------------------------------------------ //
    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs arg)
{
    var topic = arg.ApplicationMessage.Topic;
    var payload = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);

    if (topic.Contains("zigbee2mqtt/bridge"))
    {
        Console.WriteLine("[DEBUG] Ignoring bridge topic.");
        return;
    }

    // Example availability topic:
    // zigbee2mqtt/<device_address>/availability
    var isAvailabilityTopic = topic.EndsWith("/availability");

    // Extract device address
    var deviceAddress = topic
        .Replace("zigbee2mqtt/", "")
        .Replace("/availability", "");

    using var scope = scopeFactory.CreateScope();

    var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
    var filterService = scope.ServiceProvider.GetRequiredService<IDeviceFilterService>();

    // Check subscription
    if (!await sub.IsSubscribedAsync(deviceAddress))
    {
        Console.WriteLine($"[DEBUG] Device {deviceAddress} is not subscribed. Skipping.");
        return;
    }

    Console.WriteLine($"[DEBUG] Device {deviceAddress} is subscribed.");

    var device = await deviceService.GetDeviceByAddressAsync(deviceAddress, allowNull: true);
    if (device == null)
    {
        Console.WriteLine("[DEBUG] Device is null.");
        return;
    }


    if (isAvailabilityTopic && payload.Contains("\"offline\""))
    {
        Console.WriteLine($"[DEBUG] Device {deviceAddress} is offline.");

        await deviceService.SetUnsubscribedAsync(deviceAddress);
        await deviceService.SetRemovedAsync(deviceAddress);


        return;
    }
        

        // Query filters for this device
        var keys = await filterService.QueryDataFilterAsync(deviceAddress);

        var node = JsonNode.Parse(payload)?.AsObject();
        if (node == null)
        {
            Console.WriteLine("[DEBUG] Payload JSON could not be parsed.");
            return;
        }
        Console.WriteLine($"[DEBUG] Message Parsed.");
        // Handle zero sensor values if present
        if (node.ContainsKey("temperature") && node.ContainsKey("co2_2") && node.ContainsKey("humidity"))
        {
            var tempNode = node["temperature"];
            var co2Node = node["co2_2"];
            var humidityNode = node["humidity"];
            Console.WriteLine(tempNode);
            Console.WriteLine($"[DEBUG] Sending Temp to OT.");
            await openTherm.SendParameterAsync("currentTemp", tempNode);
            Console.WriteLine($"[DEBUG] Sent!.");

            if (IsZeroValue(tempNode) && IsZeroValue(co2Node) && IsZeroValue(humidityNode))
            {
                Console.WriteLine("[DEBUG] Zero sensor values detected. Handling.");
                await HandleZeroSensorValuesAsync(deviceAddress);
            }
        }

        // Filter data
        var filtered = new JsonObject();

        if (keys != null && keys.Any())
        {
            // Only check co2 if it exists, weird interaction
            bool co2IsZero = node.ContainsKey("co2_2") && IsZeroValue(node["co2_2"]);

            if (!co2IsZero) 
            {
                foreach (var k in keys)
                {
                    if (node.ContainsKey(k))
                        filtered[k] = node[k]!.DeepClone();
                }
            }
        }
        else
        {
            foreach (var kv in node)
            {
                Console.WriteLine($"[DEBUG] Adding filter for key: {kv.Key}");
                await filterService.NewFilterEntryAsync(deviceAddress, kv.Key);
            }
        }

        Console.WriteLine($"[DEBUG] Filtered data for device {deviceAddress}: {filtered.ToJsonString()}");

        // Send telemetry
        foreach (var ft in filtered)
        {
            Console.WriteLine($"[DEBUG] Sending telemetry: {ft.Key} = {ft.Value}");
            await azureService.SendTelemetryAsync(deviceAddress, ft.Key, ft.Value);
        }

        // Handle late options if present
        if (_lateOptions.TryGetValue(deviceAddress, out var pending))
        {
            Console.WriteLine($"[DEBUG] Late options received for {deviceAddress}. Processing...");
            await LateOptionAsync(payload, pending.Address, pending.ReadableProps, pending.Descriptions);
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

        int tempChange =
            int.Parse(configs.FirstOrDefault(c => IsAttributeMatch(c, "temperature"))?.ReportableChange ?? "0");
        int humidityChange =
            int.Parse(configs.FirstOrDefault(c => IsAttributeMatch(c, "humidity"))?.ReportableChange ?? "0");
        int co2Change = int.Parse(configs.FirstOrDefault(c => IsAttributeMatch(c, "co2_2"))?.ReportableChange ?? "0");


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
