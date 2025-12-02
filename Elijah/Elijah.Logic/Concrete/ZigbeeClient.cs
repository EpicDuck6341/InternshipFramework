using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elijah.Logic.Abstract;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Protocol;

namespace Elijah.Logic.Concrete;

public class ZigbeeClient(
    IMqttConnectionService conn,
    ISubscriptionService sub,
    ISendService send,
    IReceiveService receive,
    IServiceScopeFactory scopeFactory
) : IZigbeeClient
{
    
    public async Task ConnectToMqtt()
    {
        await conn.ConnectAsync();
    }
    
    public async Task SendReportConfig()
    {
        using var scope = scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var configuredReportingsService = scope.ServiceProvider.GetRequiredService<IConfiguredReportingsService>();

        var changed =
            await configuredReportingsService.GetChangedReportConfigsAsync(await deviceService.GetSubscribedAddressesAsync());
        await send.SendReportConfigAsync(changed);
    }

    public async Task SendDeviceOptions()
    {
        using var scope = scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
        var optionService = scope.ServiceProvider.GetRequiredService<IOptionService>();

        var changed = await optionService.GetChangedOptionValuesAsync(
            await deviceService.GetSubscribedAddressesAsync());
        await send.SendDeviceOptionsAsync(changed);
    }

    public async Task AllowJoinAndListen(int seconds)
    {
        using var scope = scopeFactory.CreateScope();
        var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        await conn.Client.SubscribeAsync("zigbee2mqtt/bridge/event");
        Console.WriteLine("Subscribed to bridge");
        await send.PermitJoinAsync(seconds);

        var joinedDevice = new Queue<(string address, string model)>();
        var targetData =
            new Queue<(string address, string model, List<string> props, List<string> descs)>();

        async Task OnInterviewAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            if (e.ApplicationMessage.Topic != "zigbee2mqtt/bridge/event")
                return;

            var payloadStr = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            using var json = JsonDocument.Parse(payloadStr);
            var root = json.RootElement;

            if (
                root.GetProperty("type").GetString() != "device_interview"
                || root.GetProperty("data").GetProperty("status").GetString() != "successful"
            )
                return;

            var data = root.GetProperty("data");
            var address = data.GetProperty("ieee_address").GetString();
            var model = data.GetProperty("definition").GetProperty("model").GetString();

            Console.WriteLine($"Device joined: {address} ({model})");

            if (model != null && address != null && !await deviceService.DevicePresentAsync(model, address))
            {
                await deviceService.NewDeviceEntryAsync(model, address, address);
            }
            else
            {
                Console.WriteLine("Set to active");
            }


            var exposes = data.GetProperty("definition").GetProperty("exposes");
            var options = data.GetProperty("definition").GetProperty("options");

            var properties = new List<string>();
            var descriptions = new List<string>();

            foreach (var ex in exposes.EnumerateArray())
            {
                var access = ex.GetProperty("access").GetInt16();
                if (access is 2 or 7)
                {
                    properties.Add(ex.GetProperty("property").GetString() ?? "");
                    descriptions.Add(ex.GetProperty("description").GetString() ?? "");

                }
            }

            foreach (var opt in options.EnumerateArray())
            {
                var access = opt.GetProperty("access").GetInt16();
                if (access is 2 or 7)
                {
                    properties.Add(opt.GetProperty("property").GetString() ?? "");
                    descriptions.Add(opt.GetProperty("description").GetString() ?? "");
                    Console.WriteLine(opt.GetProperty("property").GetString());
                }
            }

            joinedDevice.Enqueue((address!, model!));
            targetData.Enqueue((address!, model!, properties, descriptions));
        }

        conn.Client.ApplicationMessageReceivedAsync += OnInterviewAsync;

        await Task.Delay(seconds * 1000);

        await send.CloseJoinAsync();

        while (joinedDevice.Count > 0)
        {
            var (addr, mdl) = joinedDevice.Dequeue();
            await GetDeviceDetails(addr, mdl);
            await sub.SubscribeAsync(addr);
        }

        await Task.Delay(500);
        while (targetData.Count > 0)
        {
            var (addr, mdl, props, descriptions) = targetData.Dequeue();
            await GetOptionDetails(addr, mdl, props, descriptions);
        }

        conn.Client.ApplicationMessageReceivedAsync -= OnInterviewAsync;
    }

    public async Task RemoveDevice(string name)
    {
        using var scope = scopeFactory.CreateScope();
        var deviceScope = scope.ServiceProvider.GetRequiredService<IDeviceService>();

        var address = await deviceScope.QueryDeviceAddressAsync(name);
        if (address != null)
        {
            var device  = await deviceScope.GetDeviceByAdressAsync(address);
            if (device != null)
            {
                device.SysRemoved = true;
                device.Subscribed = false;
            }
        }

        if (address != null) await send.RemoveDeviceAsync(address);
    }

    public void StartProcessingMessages() => receive.StartMessageLoop();


    public async Task GetDeviceDetails(string address, string modelId)
    {
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
                if (device.GetProperty("ieee_address").GetString() != address)
                    continue;

                if (!device.TryGetProperty("endpoints", out var endpoints))
                    continue;

                foreach (var ep in endpoints.EnumerateObject())
                {
                    const int maxRetries = 5;
                    int retries = 0;
                    while (retries < maxRetries)
                    {
                        if (
                            ep.Value.TryGetProperty("configured_reportings", out var reportings)
                            && reportings.GetArrayLength() > 0
                        )
                        {
                            foreach (var rep in reportings.EnumerateArray())
                            {
                                await configuredReportings.NewConfigRepEntryAsync(
                                    address,
                                    rep.GetProperty("cluster").GetString(),
                                    rep.GetProperty("attribute").GetString(),
                                    rep.GetProperty("maximum_report_interval")
                                        .GetInt32()
                                        .ToString(),
                                    rep.GetProperty("minimum_report_interval")
                                        .GetInt32()
                                        .ToString(),
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
                                .WithPayload(
                                    JsonSerializer.Serialize(
                                        new { transaction = Guid.NewGuid().ToString() }
                                    )
                                )
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
                .WithPayload(
                    JsonSerializer.Serialize(new { transaction = Guid.NewGuid().ToString() })
                )
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build()
        );

        await tcs.Task;
    }

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

        var changed = await configuredReportings.GetAllReportConfigsForAddressAsync(address);
        await send.SendReportConfigAsync(changed);


        // Subscribe to the state topic
        conn.Client.ApplicationMessageReceivedAsync += Handler;
        
        await Task.Delay(50);
        
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(15000));
        if (completed != tcs.Task)
        {
            Console.WriteLine($"Timeout while getting options for {address}");

            // Store for later handling
            var receiveService= scope.ServiceProvider.GetRequiredService<IReceiveService>()
                as ReceiveService;

            receiveService?.RegisterLateOption(address, model, readableProps, descriptions);

            conn.Client.ApplicationMessageReceivedAsync -= Handler;
        }
    }

    
    public Task subscribeToAll() => Task.FromResult(sub.SubscribeAllActiveDevicesAsync());
}