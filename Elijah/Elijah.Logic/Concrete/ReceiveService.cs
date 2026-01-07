using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elijah.Domain.Models;
using Elijah.Logic.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MQTTnet;
using MQTTnet.Protocol;
using Microsoft.Extensions.Logging;
using FacilicomLogManager.Extensions;

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
    IOpenThermService openTherm,
    ILogger<ReceiveService> logger) : IReceiveService, IHostedService
{
    private bool _isRunning = false;
    private readonly object _lock = new object();
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                logger
                    .WithFacilicomContext(friendlyMessage: $"ReceiveService al actief")
                    .SendLogWarning("ReceiveService already running");
                return Task.CompletedTask;
            }

            _isRunning = true;
        }

        logger
            .WithFacilicomContext(friendlyMessage: $"ReceiveService starten")
            .SendLogInformation("ReceiveService starting automatically...");
        StartMessageLoop(); 
        return Task.CompletedTask;
    }

 
    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"ReceiveService stoppen")
            .SendLogInformation("ReceiveService stopped");
        mqtt.Client.ApplicationMessageReceivedAsync -= OnMessageAsync;
        return Task.CompletedTask;
    }
    
    private async void StartMessageLoop()
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Wachten op MQTT verbinding")
            .SendLogInformation("StartMessageLoop started");

        int attempts = 0;
        while (!mqtt.Client.IsConnected && attempts < 30)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Wachten op verbinding... ({attempts + 1}/30)")
                .SendLogInformation("Waiting for MQTT connection... ({Attempt}/30)", attempts + 1);
            await Task.Delay(1000);
            attempts++;
        }

        if (!mqtt.Client.IsConnected)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"MQTT niet verbonden na 30 seconden")
                .SendLogError("MQTT not connected after 30 seconds!");
            return;
        }

        logger
            .WithFacilicomContext(friendlyMessage: $"MQTT verbonden, starten met verwerken")
            .SendLogInformation("MQTT connected - starting message processing");
        mqtt.Client.ApplicationMessageReceivedAsync += OnMessageAsync;
        
        sub.SubscribeAllActiveDevicesAsync();

        logger
            .WithFacilicomContext(friendlyMessage: $"Berichtverwerking actief")
            .SendLogInformation("Message processing is ACTIVE");
    }

    // ------------------------------------------------------- //
    // Tracks devices that timed out during option retrieval   //
    // ------------------------------------------------------- //
    private readonly Dictionary<string, PendingOptionData> _lateOptions
        = new Dictionary<string, PendingOptionData>();
    
    // ------------------------------------------ //
    // Main message handler for all MQTT messages //
    // ------------------------------------------ //
    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs arg)
    {
        var topic = arg.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(arg.ApplicationMessage.Payload);
        
        logger
            .WithFacilicomContext(friendlyMessage: $"MQTT bericht ontvangen")
            .SendLogInformation("OnMessageAsync - Topic: {Topic}, Payload: {Payload}", topic, payload);

        if (topic.Contains("zigbee2mqtt/bridge"))
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Bridge bericht genegeerd")
                .SendLogInformation("Bridge message ignored");
            return;
        }
        

        var deviceAddress = topic.Replace("zigbee2mqtt/", "");

       
        using var scope = scopeFactory.CreateScope();

        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var filterService = scope.ServiceProvider.GetRequiredService<IDeviceFilterService>();
        
        if (!await sub.IsSubscribedAsync(deviceAddress))
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Device {deviceAddress} niet geabonneerd")
                .SendLogInformation("Device {DeviceAddress} is not subscribed. Skipping.", deviceAddress);
            return;
        }
        
        var device = await deviceService.GetDeviceByAddressAsync(deviceAddress, allowNull: true);
        if (device == null)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Device is null")
                .SendLogInformation("Device is null voor address: {Address}", deviceAddress);
            return;
        }
        
        
        var keys = await filterService.QueryDataFilterAsync(deviceAddress);

        var node = JsonNode.Parse(payload)?.AsObject();
        if (node == null)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Payload JSON kon niet worden geparset")
                .SendLogWarning("Payload JSON could not be parsed.");
            return;
        }
        if (node.ContainsKey("temperature") && node.ContainsKey("co2_2") && node.ContainsKey("humidity"))
        {
            var tempNode = node["temperature"];
            var co2Node = node["co2_2"];
            var humidityNode = node["humidity"];
            logger
                .WithFacilicomContext(friendlyMessage: $"Temperatuur doorsturen naar OpenTherm")
                .SendLogInformation("Sending Temp to OT - Temp: {Temp}", tempNode);
            await openTherm.SendParameterAsync("currentTemp", tempNode);

            if (IsZeroValue(tempNode) && IsZeroValue(co2Node) && IsZeroValue(humidityNode))
            {
                logger
                    .WithFacilicomContext(friendlyMessage: $"Nul sensor waarden gedetecteerd")
                    .SendLogWarning("Zero sensor values detected. Handling.");
                await HandleZeroSensorValuesAsync(deviceAddress);
            }
        }
        
        var filtered = new JsonObject();

        if (keys != null && keys.Any())
        {
            
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
                logger
                    .WithFacilicomContext(friendlyMessage: $"Filter toevoegen voor key: {kv.Key}")
                    .SendLogInformation("Adding filter for key: {Key}", kv.Key);
                await filterService.NewFilterEntryAsync(deviceAddress, kv.Key);
            }
        }
        
        foreach (var ft in filtered)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Telemetrie versturen: {ft.Key}")
                .SendLogInformation("Sending telemetry: {Key} = {Value}", ft.Key, ft.Value);
            await azureService.SendTelemetryAsync(deviceAddress, ft.Key, ft.Value);
        }
        
        if (_lateOptions.TryGetValue(deviceAddress, out var pending))
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Late options ontvangen voor {deviceAddress}")
                .SendLogInformation("Processing late options for {Address}", deviceAddress);
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

        logger
            .WithFacilicomContext(friendlyMessage: $"Nul sensor waarden gedetecteerd voor {deviceAddress}")
            .SendLogWarning("ALL ZERO SENSOR VALUES detected for device {DeviceAddress} - reconfiguring...", deviceAddress);

        var configs = await config.ConfigByAddress(deviceAddress);
        if (!configs.Any())
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Geen reporting configs gevonden")
                .SendLogWarning("Geen reporting configs found for device {DeviceAddress}", deviceAddress);
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

        logger
            .WithFacilicomContext(friendlyMessage: $"Herconfiguratie voltooid voor {deviceAddress}")
            .SendLogInformation("Herconfiguratie voltooid voor {DeviceAddress}", deviceAddress);
    }

    // ------------------------------------------------------------------------------- //
    // Sends a single configuration value to ESP via MQTT using proper JSON structure  //
    // ------------------------------------------------------------------------------- //
    private async Task SendConfigValueAsync(string address, string parameterName, int value)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Config versturen: {parameterName}={value}")
            .SendLogInformation("SendConfigValueAsync - Address: {Address}, Parameter: {Parameter}, Value: {Value}", address, parameterName, value);

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
        logger
            .WithFacilicomContext(friendlyMessage: $"Config verstuurd")
            .SendLogInformation("Sent config: {Address} -> {Parameter}={Value}", address, parameterName, value);
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
        if (node == null) 
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Payload kon niet worden geparset")
                .SendLogWarning("Node is null in LateOptionAsync");
            return;
        }

        for (int i = 0; i < readableProps.Count; i++)
        {
            var prop = readableProps[i];
            var value = node[prop]?.ToJsonString() ?? "-";
            await option.SetOptionsAsync(address, descriptions[i], value, prop);
            logger
                .WithFacilicomContext(friendlyMessage: $"Late option verwerkt: {prop}")
                .SendLogInformation("(LATE) Option: {Property} = {Value}", prop, value);
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

        logger
            .WithFacilicomContext(friendlyMessage: $"Late options opgeslagen voor {address}")
            .SendLogInformation("Stored late options for {Address}", address);
    }
}