using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elijah.Logic.Abstract;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Protocol;

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
    IServiceScopeFactory scopeFactory
) : IZigbeeClient
{
    // ----------------------------------- //
    // Establishes MQTT broker connection  //
    // ----------------------------------- //
    public async Task ConnectToMqtt()
    {
        await conn.ConnectAsync();
    }
    
    // ------------------------------------------------------ //
    // Sends changed reporting configurations to all devices  //
    // ------------------------------------------------------ //
    public async Task SendReportConfig()
    {
        using var scope = scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var configuredReportingsService = scope.ServiceProvider.GetRequiredService<IConfiguredReportingsService>();

        var changed = await configuredReportingsService.GetChangedReportConfigsAsync(
            await deviceService.GetSubscribedAddressesAsync());
        await send.SendReportConfigAsync(changed);
    }

    // ------------------------------------------------------- //
    // Sends changed device options to all subscribed devices  //
    // ------------------------------------------------------- //
    public async Task SendDeviceOptions()
    {
        using var scope = scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var optionService = scope.ServiceProvider.GetRequiredService<IOptionService>();

        var changed = await optionService.GetChangedOptionValuesAsync(
            await deviceService.GetSubscribedAddressesAsync());
        await send.SendDeviceOptionsAsync(changed);
    }

    // ------------------------------------------------------- //
    // Enables device joining and processes interview results  //
    // ------------------------------------------------------- //
    public async Task AllowJoinAndListen(int seconds)
{
    using var scope = scopeFactory.CreateScope();
    var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();

    await conn.Client.SubscribeAsync("zigbee2mqtt/bridge/event");
    Console.WriteLine($"DEBUG: Permitting join for {seconds} seconds."); // DEBUG
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
        
        Console.WriteLine($"Device joined: {address} ({model})"); // Original line
        string type = await deviceService.DevicePresentAsync(model, address);
        if (type.Equals("TemplateNotExist"))
        {
            Console.WriteLine($"DEBUG: New device ({model}, {address}). Creating new entry."); // DEBUG
            await deviceService.NewDeviceEntryAsync(model, address, address);
        }
        else if(type.Equals("deviceExist"))
        {
            Console.WriteLine($"DEBUG: Device ({model}, {address}) already present or data is null. Set to active."); // DEBUG
            if (address != null)
            {
                Console.WriteLine($"DEBUG: Subscribing to existing device address: {address}"); // DEBUG
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
                Console.WriteLine($"DEBUG: Processing expose. Access: {access}"); // DEBUG
                if (access is 2 or 7)
                {
                    var prop = ex.GetProperty("property").GetString();
                    var desc = ex.GetProperty("description").GetString();
                    properties.Add(prop ?? "");
                    descriptions.Add(desc ?? "");
                    Console.WriteLine($"DEBUG: Added property (expose): {prop}"); // DEBUG
                }
            }
        }

        if (options.GetArrayLength() != 0)
        {
            foreach (var opt in options.EnumerateArray())
            {
                var access = opt.GetProperty("access").GetInt16();
                Console.WriteLine($"DEBUG: Processing option. Access: {access}"); // DEBUG
                if (access is 2 or 7)
                {
                    var prop = opt.GetProperty("property").GetString();
                    var desc = opt.GetProperty("description").GetString();
                    properties.Add(prop ?? "");
                    descriptions.Add(desc ?? "");
                    Console.WriteLine(opt.GetProperty("property").GetString()); // Original line
                    Console.WriteLine($"DEBUG: Added property (option): {prop}"); // DEBUG
                }
            }
        }


        if (properties.Count != 0&&descriptions.Count != 0)
        {
            targetData.Enqueue((address!, model!, properties, descriptions));
        }

        Console.WriteLine($"DEBUG: Device ({address}) enqueued for processing."); // DEBUG
    }

    conn.Client.ApplicationMessageReceivedAsync += OnInterviewAsync;
    Console.WriteLine($"DEBUG: Attached OnInterviewAsync handler. Waiting for {seconds} seconds."); // DEBUG
    await Task.Delay(seconds * 1000);
    Console.WriteLine("DEBUG: Wait time expired. Closing join."); // DEBUG
    await send.CloseJoinAsync();

    // Process joined devices
    Console.WriteLine($"DEBUG: Starting to process joined devices. Count: {joinedDevice.Count}"); // DEBUG
    while (joinedDevice.Count > 0)
    {
        var (addr, mdl) = joinedDevice.Dequeue();
        Console.WriteLine($"DEBUG: Dequeued device for details: {addr} ({mdl})"); // DEBUG
        await GetDeviceDetails(addr, mdl);
        Console.WriteLine($"DEBUG: Subscribing to device: {addr}"); // DEBUG
        await sub.SubscribeAsync(addr);
    }
    Console.WriteLine("DEBUG: Finished processing joined devices."); // DEBUG

    await Task.Delay(500);
    Console.WriteLine("DEBUG: Short delay after initial processing."); // DEBUG

    // Process device options
    Console.WriteLine($"DEBUG: Starting to process device options. Count: {targetData.Count}"); // DEBUG
    while (targetData.Count > 0)
    {
        var (addr, mdl, props, descriptions) = targetData.Dequeue();
        Console.WriteLine($"DEBUG: Dequeued device for options: {addr} ({mdl}) with {props.Count} properties."); // DEBUG
        await GetOptionDetails(addr, mdl, props, descriptions);
    }
    Console.WriteLine("DEBUG: Finished processing device options."); // DEBUG

    conn.Client.ApplicationMessageReceivedAsync -= OnInterviewAsync;
    Console.WriteLine("DEBUG: Detached OnInterviewAsync handler."); // DEBUG
    Console.WriteLine("DEBUG: AllowJoinAndListen finished."); // DEBUG
}

    // ---------------------------------------------------- //
    // Marks a device as removed and sends removal command  //
    // ---------------------------------------------------- //
    public async Task RemoveDevice(string name)
    {
        using var scope = scopeFactory.CreateScope();
        var deviceScope = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        var address = await deviceScope.QueryDeviceAddressAsync(name);
        if (address != null)
        {
            var device = await deviceScope.GetDeviceByAddressAsync(address);
            if (device != null)
            {
                await deviceScope.SetUnsubscribedAsync(device.Address);
                await deviceScope.SetRemovedAsync(device.Address);
            }
        }

        if (address != null) await send.RemoveDeviceAsync(address);
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
        using var scope = scopeFactory.CreateScope();
        var configuredReportings = scope.ServiceProvider.GetRequiredService<IConfiguredReportingsService>();
        var tcs = new TaskCompletionSource<bool>();

        // Handler for device details response
        async Task Handler(MqttApplicationMessageReceivedEventArgs e)
        {
            if (e.ApplicationMessage.Topic != "zigbee2mqtt/bridge/devices")
                return;

            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
            
            foreach (var device in doc.RootElement.EnumerateArray())
            {
                if (device.GetProperty("ieee_address").GetString() != address)
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

                            conn.Client.ApplicationMessageReceivedAsync -= Handler;
                            tcs.TrySetResult(true);
                            return;
                        }

                        retries++;
                        await Task.Delay(5_000);
                        
                        await conn.Client.PublishAsync(
                            new MqttApplicationMessageBuilder()
                                .WithTopic("zigbee2mqtt/bridge/request/devices")
                                .WithPayload(JsonSerializer.Serialize(new { transaction = Guid.NewGuid().ToString() }))
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
                .WithPayload(JsonSerializer.Serialize(new { transaction = Guid.NewGuid().ToString() }))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build()
        );

        await tcs.Task;
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
            Console.WriteLine($"Received payload from {address}: {payload}");

            JsonNode? node = JsonNode.Parse(payload);
            if (node == null)
                return;

            // Process all readable properties
            for (int i = 0; i < readableProps.Count; i++)
            {
                var prop = readableProps[i];
                var value = node[prop]?.ToJsonString() ?? "-";
                await option.SetOptionsAsync(address, descriptions[i], value, prop);
                Console.WriteLine($"Option: {prop} = {value}");
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
            Console.WriteLine($"Timeout while getting options for {address}");

            var receiveService = scope.ServiceProvider.GetRequiredService<IReceiveService>() as ReceiveService;
            receiveService?.RegisterLateOption(address, model, readableProps, descriptions);
            var config = await configuredReportings.ConfigByAddress(address);
            await send.SendReportConfigAsync(config);

            conn.Client.ApplicationMessageReceivedAsync -= Handler;
        }
    }

    // --------------------------------- //
    // Subscribes to all active devices  //
    // --------------------------------- //
    public Task SubscribeToAll() => Task.FromResult(sub.SubscribeAllActiveDevicesAsync());
}
