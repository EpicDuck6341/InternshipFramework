using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elijah.Logic.Abstract;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Protocol;
using Microsoft.Extensions.Logging;
using FacilicomLogManager.Extensions;

namespace Elijah.Logic.Concrete;

// ----------------------------------------------- //
// Main Zigbee client orchestrator                 //
// Coordinates all services for device management  //
// ----------------------------------------------- //
public class ZigbeeClient(
    IMqttConnectionService conn,
    ISubscriptionService sub,
    ISendService send,
    IReceiveService receive,
    IServiceScopeFactory scopeFactory,
    ILogger<ZigbeeClient> logger) : IZigbeeClient
{
    // ----------------------------------- //
    // Establishes MQTT broker connection  //
    // ----------------------------------- //
    public async Task ConnectToMqtt()
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Verbinden met MQTT")
            .SendLogInformation("ConnectToMqtt started");
        await conn.ConnectAsync();
        logger
            .WithFacilicomContext(friendlyMessage: $"MQTT verbinding voltooid")
            .SendLogInformation("ConnectToMqtt voltooid");
    }
    
    // ------------------------------------------------------ //
    // Sends changed reporting configurations to all devices  //
    // ------------------------------------------------------ //
    public async Task SendReportConfig()
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Reporting configs versturen")
            .SendLogInformation("SendReportConfig started");

        using var scope = scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var configuredReportingsService = scope.ServiceProvider.GetRequiredService<IConfiguredReportingsService>();

        var changed = await configuredReportingsService.GetChangedReportConfigsAsync(
            await deviceService.GetSubscribedAddressesAsync());
        await send.SendReportConfigAsync(changed);
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Reporting configs versturen voltooid")
            .SendLogInformation("SendReportConfig voltooid - Aantal configs: {Count}", changed.Count);
    }

    // ------------------------------------------------------- //
    // Sends changed device options to all subscribed devices  //
    // ------------------------------------------------------- //
    public async Task SendDeviceOptions()
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Device options versturen")
            .SendLogInformation("SendDeviceOptions started");

        using var scope = scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var optionService = scope.ServiceProvider.GetRequiredService<IOptionService>();

        var changed = await optionService.GetChangedOptionValuesAsync(
            await deviceService.GetSubscribedAddressesAsync());
        await send.SendDeviceOptionsAsync(changed);
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Device options versturen voltooid")
            .SendLogInformation("SendDeviceOptions voltooid - Aantal options: {Count}", changed.Count);
    }

    // ------------------------------------------------------- //
    // Enables device joining and processes interview results  //
    // ------------------------------------------------------- //
    public async Task AllowJoinAndListen(int seconds)
{
    logger
        .WithFacilicomContext(friendlyMessage: $"Join inschakelen voor {seconds} seconden")
        .SendLogInformation("AllowJoinAndListen started - Seconds: {Seconds}", seconds);

    using var scope = scopeFactory.CreateScope();
    var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();

    await conn.Client.SubscribeAsync("zigbee2mqtt/bridge/event");
    logger
        .WithFacilicomContext(friendlyMessage: $"Join inschakelen")
        .SendLogInformation("Permitting join for {Seconds} seconds.", seconds);
    await send.PermitJoinAsync(seconds);

    var joinedDevice = new Queue<(string address, string model)>();
    var targetData = new Queue<(string address, string model, List<string> props, List<string> descs)>();
    
    async Task OnInterviewAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        if (e.ApplicationMessage.Topic != "zigbee2mqtt/bridge/event")
            return;

        var payloadStr = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
        using var json = JsonDocument.Parse(payloadStr);
        var root = json.RootElement;

        

        var data = root.GetProperty("data");
        var address = data.GetProperty("ieee_address").GetString();
        var model = data.GetProperty("definition").GetProperty("model").GetString();
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Device toegetreden: {address} ({model})")
            .SendLogInformation("Device joined: {Address} ({Model})", address, model);
        string type = await deviceService.DevicePresentAsync(model, address);
        logger
            .WithFacilicomContext(friendlyMessage: $"Resultaat: {type}")
            .SendLogInformation("DevicePresentAsync result: {Type}", type);
        if (type.Equals("templateNotExist"))
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Nieuwe device aanmaken: {model}, {address}")
                .SendLogInformation("Creating new device entry");
            await deviceService.NewDeviceEntryAsync(model, address, address);
        }
        else if(type.Equals("deviceExist"))
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Device bestaat al, activeren")
                .SendLogInformation("Device already present or data is null. Subscribing to existing device.");
            if (address != null)
            {
                logger
                    .WithFacilicomContext(friendlyMessage: $"Subscriben naar bestaand device")
                    .SendLogInformation("Subscribing to existing device address: {Address}", address);
                await sub.SubscribeAsync(address);
            }
            return;
        }
        
        joinedDevice.Enqueue((address!, model!));

        var exposes = data.GetProperty("definition").GetProperty("exposes");
        var options = data.GetProperty("definition").GetProperty("options");

        var properties = new List<string>();
        var descriptions = new List<string>();

        if (exposes.GetArrayLength() != 0)
        {
            foreach (var ex in exposes.EnumerateArray())
            {
                var access = ex.GetProperty("access").GetInt16();
                logger
                    .WithFacilicomContext(friendlyMessage: $"Verwerken expose")
                    .SendLogInformation("Processing expose. Access: {Access}", access);
                if (access is 2 or 7)
                {
                    var prop = ex.GetProperty("property").GetString();
                    var desc = ex.GetProperty("description").GetString();
                    properties.Add(prop ?? "");
                    descriptions.Add(desc ?? "");
                    logger
                        .WithFacilicomContext(friendlyMessage: $"Property toegevoegd: {prop}")
                        .SendLogInformation("Added property (expose): {Property}", prop);
                }
            }
        }

        if (options.GetArrayLength() != 0)
        {
            foreach (var opt in options.EnumerateArray())
            {
                var access = opt.GetProperty("access").GetInt16();
                logger
                    .WithFacilicomContext(friendlyMessage: $"Verwerken option")
                    .SendLogInformation("Processing option. Access: {Access}", access);
                if (access is 2 or 7)
                {
                    var prop = opt.GetProperty("property").GetString();
                    var desc = opt.GetProperty("description").GetString();
                    properties.Add(prop ?? "");
                    descriptions.Add(desc ?? "");
                    Console.WriteLine(opt.GetProperty("property").GetString());
                    logger
                        .WithFacilicomContext(friendlyMessage: $"Property toegevoegd: {prop}")
                        .SendLogInformation("Added property (option): {Property}", prop);
                }
            }
        }


        if (properties.Count != 0&&descriptions.Count != 0)
        {
            targetData.Enqueue((address!, model!, properties, descriptions));
        }

        logger
            .WithFacilicomContext(friendlyMessage: $"Device in wachtrij geplaatst")
            .SendLogInformation("Device ({Address}) enqueued for processing.", address);
    }

    conn.Client.ApplicationMessageReceivedAsync += OnInterviewAsync;
    logger
        .WithFacilicomContext(friendlyMessage: $"Handler aangekoppeld, wachten...")
        .SendLogInformation("Attached OnInterviewAsync handler. Waiting for {Seconds} seconds.", seconds);
    await Task.Delay(seconds * 1000);
    logger
        .WithFacilicomContext(friendlyMessage: $"Wachttijd verlopen")
        .SendLogInformation("Wait time expired. Closing join.");
    await send.CloseJoinAsync();

    // Process joined devices
    logger
        .WithFacilicomContext(friendlyMessage: $"Verwerken toegetreden devices")
        .SendLogInformation("Starting to process joined devices. Count: {Count}", joinedDevice.Count);
    while (joinedDevice.Count > 0)
    {
        var (addr, mdl) = joinedDevice.Dequeue();
        logger
            .WithFacilicomContext(friendlyMessage: $"Device details ophalen")
            .SendLogInformation("Dequeued device for details: {Address} ({Model})", addr, mdl);
        await GetDeviceDetails(addr, mdl);
        logger
            .WithFacilicomContext(friendlyMessage: $"Subscriben naar device")
            .SendLogInformation("Subscribing to device: {Address}", addr);
        await sub.SubscribeAsync(addr);
    }
    logger
        .WithFacilicomContext(friendlyMessage: $"Verwerken devices voltooid")
        .SendLogInformation("Finished processing joined devices.");

    await Task.Delay(500);
    logger
        .WithFacilicomContext(friendlyMessage: $"Korte pauze")
        .SendLogInformation("Short delay after initial processing.");

    // Process device options
    logger
        .WithFacilicomContext(friendlyMessage: $"Verwerken device options")
        .SendLogInformation("Starting to process device options. Count: {Count}", targetData.Count);
    while (targetData.Count > 0)
    {
        var (addr, mdl, props, descriptions) = targetData.Dequeue();
        logger
            .WithFacilicomContext(friendlyMessage: $"Device options verwerken")
            .SendLogInformation("Dequeued device for options: {Address} ({Model}) with {Count} properties.", addr, mdl, props.Count);
        await GetOptionDetails(addr, mdl, props, descriptions);
    }
    logger
        .WithFacilicomContext(friendlyMessage: $"Device options verwerking voltooid")
        .SendLogInformation("Finished processing device options.");

    conn.Client.ApplicationMessageReceivedAsync -= OnInterviewAsync;
    logger
        .WithFacilicomContext(friendlyMessage: $"Handler losgekoppeld")
        .SendLogInformation("Detached OnInterviewAsync handler.");
    logger
        .WithFacilicomContext(friendlyMessage: $"AllowJoinAndListen voltooid")
        .SendLogInformation("AllowJoinAndListen finished.");
}

    // ---------------------------------------------------- //
    // Marks a device as removed and sends removal command  //
    // ---------------------------------------------------- //
    public async Task RemoveDevice(string name)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Device verwijderen: {name}")
            .SendLogInformation("RemoveDevice started - Name: {Name}", name);

        using var scope = scopeFactory.CreateScope();
        var deviceScope = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        var address = await deviceScope.QueryDeviceAddressAsync(name);
        if (address != null)
        {
            var device = await deviceScope.GetDeviceByAddressAsync(address);
            if (device != null)
            {
                device.SysRemoved = true;
                device.Subscribed = false;
            }
        }

        if (address != null) 
        {
            await send.RemoveDeviceAsync(address);
            logger
                .WithFacilicomContext(friendlyMessage: $"Device verwijder commando verstuurd")
                .SendLogInformation("RemoveDevice command verstuurd - Address: {Address}", address);
        }
    }

    // --------------------------------------------- //
    // Starts the background message processing loop //
    // --------------------------------------------- //
    // public void StartProcessingMessages() => receive.StartMessageLoop();

    // ---------------------------------------------------------- //
    // Retrieves detailed reporting config for a specific device  //
    // ---------------------------------------------------------- //
public async Task GetDeviceDetails(string address, string modelId)
{
    logger
        .WithFacilicomContext(friendlyMessage: $"Device details ophalen: {address}")
        .SendLogInformation("GetDeviceDetails started - Address: {Address}, ModelId: {ModelId}", address, modelId);

    using var scope = scopeFactory.CreateScope();
    var configuredReportings = scope.ServiceProvider.GetRequiredService<IConfiguredReportingsService>();
    var tcs = new TaskCompletionSource<bool>();

    async Task Handler(MqttApplicationMessageReceivedEventArgs e)
    {
        if (e.ApplicationMessage.Topic != "zigbee2mqtt/bridge/devices")
            return;

        using var doc = JsonDocument.Parse(
            Encoding.UTF8.GetString(e.ApplicationMessage.Payload)
        );

        foreach (var device in doc.RootElement.EnumerateArray())
        {
            var ieee = device.GetProperty("ieee_address").GetString();
            if (ieee != address)
                continue;

            if (!device.TryGetProperty("endpoints", out var endpoints))
                continue;

            foreach (var ep in endpoints.EnumerateObject())
            {
                int retries = 0;
                const int maxRetries = 5;

                while (retries < maxRetries)
                {
                    if (ep.Value.TryGetProperty("configured_reportings", out var reportings) &&
                        reportings.GetArrayLength() > 0)
                    {
                        foreach (var rep in reportings.EnumerateArray())
                        {
                            try{
                                await configuredReportings.NewConfigRepEntryAsync(
                                    address,
                                    rep.GetProperty("cluster").GetString(),
                                    rep.GetProperty("attribute").GetString(),
                                    rep.GetProperty("maximum_report_interval").GetInt32().ToString(),
                                    rep.GetProperty("minimum_report_interval").GetInt32().ToString(),
                                    rep.GetProperty("reportable_change").ToString(),
                                    ep.Name
                                );
                            }
                            catch (Exception ex)
                            {
                                logger
                                    .WithFacilicomContext(friendlyMessage: $"Fout bij opslaan reporting config")
                                    .SendLogError(ex, "Exception in NewConfigRepEntryAsync - Message: {Message}", ex.Message);
                            }
                        }

                        conn.Client.ApplicationMessageReceivedAsync -= Handler;
                        tcs.TrySetResult(true);
                        return;
                    }

                    retries++;
                    await Task.Delay(5_000);

                    await conn.Client.PublishAsync(
                        new MqttApplicationMessageBuilder()
                            .WithTopic("zigbee2mqtt/bridge/request/devices")
                            .WithPayload(JsonSerializer.Serialize(new
                            {
                                transaction = Guid.NewGuid().ToString()
                            }))
                            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                            .Build()
                    );
                }
            }
        }
    }

    conn.Client.ApplicationMessageReceivedAsync += Handler;
    await conn.Client.SubscribeAsync("zigbee2mqtt/bridge/devices");
    await conn.Client.PublishAsync(
        new MqttApplicationMessageBuilder()
            .WithTopic("zigbee2mqtt/bridge/request/devices")
            .WithPayload(JsonSerializer.Serialize(new
            {
                transaction = Guid.NewGuid().ToString()
            }))
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build()
    );

    await tcs.Task;
    
    logger
        .WithFacilicomContext(friendlyMessage: $"Device details ophalen voltooid")
        .SendLogInformation("GetDeviceDetails completed successfully");
}



    // ---------------------------------------------- //
    // Retrieves and processes device option details  //
    // ---------------------------------------------- //
    public async Task GetOptionDetails(
        string address,
        string model,
        List<string> readableProps,
        List<string> descriptions
    )
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Option details ophalen: {address}")
            .SendLogInformation("GetOptionDetails started - Address: {Address}, Model: {Model}", address, model);

        using var scope = scopeFactory.CreateScope();
        var option = scope.ServiceProvider.GetRequiredService<IOptionService>();
        var configuredReportings = scope.ServiceProvider.GetRequiredService<IConfiguredReportingsService>();
        var tcs = new TaskCompletionSource<bool>();

        // Handler for option details response
        async Task Handler(MqttApplicationMessageReceivedEventArgs e)
        {
            string topic = e.ApplicationMessage.Topic;
            if (topic != $"zigbee2mqtt/{address}")
                return;

            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            logger
                .WithFacilicomContext(friendlyMessage: $"Payload ontvangen van {address}")
                .SendLogInformation("Received payload from {Address}: {Payload}", address, payload);

            JsonNode? node = JsonNode.Parse(payload);
            if (node == null)
            {
                logger
                    .WithFacilicomContext(friendlyMessage: $"Payload kon niet worden geparset")
                    .SendLogWarning("Node is null voor payload");
                return;
            }

            // Process all readable properties
            for (int i = 0; i < readableProps.Count; i++)
            {
                var prop = readableProps[i];
                var value = node[prop]?.ToJsonString() ?? "-";
                await option.SetOptionsAsync(address, descriptions[i], value, prop);
                logger
                    .WithFacilicomContext(friendlyMessage: $"Option verwerkt: {prop}")
                    .SendLogInformation("Option: {Property} = {Value}", prop, value);
            }

            conn.Client.ApplicationMessageReceivedAsync -= Handler;
            var config = await configuredReportings.ConfigByAddress(address);
            await send.SendReportConfigAsync(config);
            tcs.TrySetResult(true);
        }

        // Send reporting config and wait for response
        var changed = await configuredReportings.GetAllReportConfigsForAddressAsync(address);
        await send.SendReportConfigAsync(changed);

        conn.Client.ApplicationMessageReceivedAsync += Handler;
        await Task.Delay(50);
        
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(15000));
        if (completed != tcs.Task)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Timeout bij ophalen options")
                .SendLogWarning("Timeout while getting options for {Address}", address);

            var receiveService = scope.ServiceProvider.GetRequiredService<IReceiveService>() as ReceiveService;
            receiveService?.RegisterLateOption(address, model, readableProps, descriptions);
            var config = await configuredReportings.ConfigByAddress(address);
            await send.SendReportConfigAsync(config);

            conn.Client.ApplicationMessageReceivedAsync -= Handler;
        }
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Option details ophalen voltooid")
            .SendLogInformation("GetOptionDetails voltooid");
    }

    // --------------------------------- //
    // Subscribes to all active devices  //
    // --------------------------------- //
    public Task SubscribeToAll() => Task.FromResult(sub.SubscribeAllActiveDevicesAsync());
}