using System.IO.Ports;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elijah.Logic.Abstract;
using MQTTnet;
using MQTTnet.Protocol;

namespace Elijah.Logic.Concrete;

public class ZigbeeClient(
    IMqttConnectionService conn,
    ISubscriptionService sub,
    SerialPort serialPort,
    ISendService send,
    IReceiveService recv,
    IDeviceService deviceService,
    IConfiguredReportingsService configuredReportings,
    IOptionService option,
    IDeviceTemplateService template
) : IZigbeeClient
{
    public bool IsReady { get; private set; }

    public async Task ConnectToMqtt()
    {
        await conn.ConnectAsync();
    }

    public async Task SubscribeDevices()
    {
        await sub.SubscribeExistingAsync();
        IsReady = true;
    }

    public async Task SubscribeAfterJoin(string a)
    {
        await sub.SubscribeAsync(a);
        IsReady = true;
    }

    public async Task SendReportConfig()
    {
        var changed = await configuredReportings.GetChangedReportConfigsAsync(
            await deviceService.GetSubscribedAddressesAsync()
        );
        await send.SendReportConfigAsync(changed);
    }

    public async Task SendDeviceOptions()
    {
        var changed = await option.GetChangedOptionValuesAsync(
            await deviceService.GetSubscribedAddressesAsync()
        );
        await send.SendDeviceOptionsAsync(changed);
    }

    public async Task AllowJoinAndListen(int seconds)
    {
        await conn.Client.SubscribeAsync("zigbee2mqtt/bridge/event");
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

            if (!await deviceService.DevicePresentAsync(model, address))
            {
                await deviceService.NewDeviceEntryAsync(model, address, address);
                await template.NewDVTemplateEntryAsync(model, address);
            }

            var exposes = data.GetProperty("definition").GetProperty("exposes");
            var options = data.GetProperty("definition").GetProperty("options");

            var props = new List<string>();
            var descs = new List<string>();

            foreach (var ex in exposes.EnumerateArray())
            {
                var access = ex.GetProperty("access").GetInt16();
                if (access is 2 or 7)
                {
                    props.Add(ex.GetProperty("property").GetString());
                    descs.Add(ex.GetProperty("description").GetString());
                }
            }

            foreach (var opt in options.EnumerateArray())
            {
                var access = opt.GetProperty("access").GetInt16();
                if (access is 2 or 7)
                {
                    props.Add(opt.GetProperty("property").GetString());
                    descs.Add(opt.GetProperty("description").GetString());
                }
            }

            joinedDevice.Enqueue((address, model));
            targetData.Enqueue((address, model, props, descs));
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

        while (targetData.Count > 0)
        {
            var (addr, mdl, props, descs) = targetData.Dequeue();
            await GetOptionDetails(addr, mdl, props, descs);
        }

        conn.Client.ApplicationMessageReceivedAsync -= OnInterviewAsync;
    }

    public async Task RemoveDevice(string name)
    {
        var device = await deviceService.GetDeviceByNameAsync(name);
        await deviceService.SetSubscribedStatusAsync(false, device.Address);
        // await _device.SetActiveStatusAsync(false, addr); REMINDER SET TO ACTIVE BUILT IN PARAMETER
        await send.RemoveDeviceAsync(device.Address);
    }

    public void StartProcessingMessages() => recv.StartMessageLoop();

    public async Task ESPConnect()
    {
        serialPort = new SerialPort("/dev/ttyUSB1", 115200);
        serialPort.ReadTimeout = 2000;
        serialPort.WriteTimeout = 2000;

        serialPort.Open();
        Console.WriteLine("Serial port opened. Waiting for ESP to reset...");
        await Task.Delay(4000);

        string response = "";
        int attempts = 0;

        while (!response.Contains("ESP_READY") && attempts < 20)
        {
            try
            {
                Console.WriteLine(attempts);
                response += serialPort.ReadExisting();
                await Task.Delay(200);
                attempts++;
            }
            catch (TimeoutException) { }
        }

        if (response.Contains("ESP_READY"))
        {
            Console.WriteLine("ESP_READY received!");
            serialPort.WriteLine("test"); // \r\n automatically added
        }
        else
        {
            Console.WriteLine("Failed to receive ESP_READY");
        }
    }

    public async Task sendESPConfig(int b)
    {
        await send.SetBrightnessAsync("0xe4b323fffe9e2d38", b);
    }

    public async Task GetDeviceDetails(string address, string modelID)
    {
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
        var tcs = new TaskCompletionSource<bool>();

        async Task Handler(MqttApplicationMessageReceivedEventArgs e)
        {
            if (e.ApplicationMessage.Topic != $"zigbee2mqtt/{address}")
                return;

            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            var node = JsonNode.Parse(payload)?.AsObject();
            if (node == null)
                return;

            for (int i = 0; i < readableProps.Count; i++)
            {
                var prop = readableProps[i];
                var desc = descriptions[i];
                if (node[prop] != null)
                {
                    await option.SetOptionsAsync(address, desc, node[prop]!.ToJsonString(), prop);
                    Console.WriteLine($"Option: {prop} = {node[prop]}");
                }
            }

            conn.Client.ApplicationMessageReceivedAsync -= Handler;
            tcs.TrySetResult(true);
        }

        conn.Client.ApplicationMessageReceivedAsync += Handler;

        await conn.Client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic($"zigbee2mqtt/{address}")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build()
        );

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(10_000));
        conn.Client.ApplicationMessageReceivedAsync -= Handler;

        if (completed != tcs.Task)
            Console.WriteLine($"Timeout while getting options for {address}");
    }
}
