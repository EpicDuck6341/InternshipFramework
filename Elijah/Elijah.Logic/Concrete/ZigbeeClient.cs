using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elijah.Data.Repository;
using Elijah.Logic.Abstract;
using Microsoft.Extensions.Configuration;
using MQTTnet;
using MQTTnet.Protocol;

namespace Elijah.Logic.Concrete;

public class ZigbeeClient(IMqttConnectionService _conn, ISubscriptionService _sub,) : IZigbeeClient
{
  
   
    private readonly SendService _send;
    private readonly ReceiveService _recv;
    private readonly IDeviceService _device;
    private readonly IConfiguredReportingsService _configuredReportings;
    private readonly IOptionService _option;
    private readonly IDeviceTemplateService _template;

    public bool IsReady { get; private set; }


    /* ---------- public façade ---------- */
    public async Task ConnectToMqtt()
    {
        await _conn.ConnectAsync();
    }

    public async Task SubscribeDevices()
    {
        await _sub.SubscribeExistingAsync();
        IsReady = true;
    }

    public async Task SubscribeAfterJoin(string a)
    {
        await _sub.SubscribeAsync(a);
        IsReady = true;
    }

    public async Task SendReportConfig()
    {
        var changed = await _configuredReportings.GetChangedReportConfigsAsync(
            await _device.GetSubscribedAddressesAsync());
        await _send.SendReportConfigAsync(changed);
    }

    public async Task SendDeviceOptions()
    {
        var changed = await _option.GetChangedOptionValuesAsync(
            await _device.GetSubscribedAddressesAsync());
        await _send.SendDeviceOptionsAsync(changed);
    }

    public async Task AllowJoinAndListen(int seconds)
    {
        // --- 1. open network for joining ---
        await _conn.Client.SubscribeAsync("zigbee2mqtt/bridge/event");
        await _send.PermitJoinAsync(seconds);

        // --- 2. queues for devices discovered during the window ---
        var joinedDevice = new Queue<(string address, string model)>();
        var targetData = new Queue<(string address, string model,
            List<string> props, List<string> descs)>();

        // --- 3. temporary handler for interview events ---
        async Task OnInterviewAsync(MqttApplicationMessageReceivedEventArgs e)
        {
            if (e.ApplicationMessage.Topic != "zigbee2mqtt/bridge/event")
                return;

            var payloadStr = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            using var json = JsonDocument.Parse(payloadStr);
            var root = json.RootElement;

            if (root.GetProperty("type").GetString() != "device_interview" ||
                root.GetProperty("data").GetProperty("status").GetString() != "successful")
                return;

            var data = root.GetProperty("data");
            var address = data.GetProperty("ieee_address").GetString();
            var model = data.GetProperty("definition").GetProperty("model").GetString();

            Console.WriteLine($"Device joined: {address} ({model})");

            // --- 4. persist device if new ---
            if (!await _device.DevicePresentAsync(model, address))
            {
                await _device.NewDeviceEntryAsync(model, address, address);
                await _template.NewDVTemplateEntryAsync(model, address);
            }

            // --- 5. collect exposes & options ---
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

        _conn.Client.ApplicationMessageReceivedAsync += OnInterviewAsync;

        // --- 6. wait for the join window ---
        await Task.Delay(seconds * 1000);

        // --- 7. close the network ---
        await _send.CloseJoinAsync();

        // --- 8. process every device that joined ---
        while (joinedDevice.Count > 0)
        {
            var (addr, mdl) = joinedDevice.Dequeue();
            await GetDeviceDetails(addr, mdl); // you already have this method stub
            await _sub.SubscribeAsync(addr);
        }

        while (targetData.Count > 0)
        {
            var (addr, mdl, props, descs) = targetData.Dequeue();
            await GetOptionDetails(addr, mdl, props, descs);
        }

        // --- 9. detach the temporary handler ---
        _conn.Client.ApplicationMessageReceivedAsync -= OnInterviewAsync;
    }
    

    public async Task RemoveDevice(string name)
    {
        var addr = await _device.QueryDeviceAddressAsync(name);
        await _device.SetSubscribedStatusAsync(false, addr);
        await _device.SetActiveStatusAsync(false, addr);
        await _send.RemoveDeviceAsync(addr);
    }

    public void StartProcessingMessages() => _recv.StartMessageLoop();

    /* ---------- serial / ESP stuff stays here ---------- */
    public async Task ESPConnect()
    {
        /* unchanged */
    }

    public async Task sendESPConfig(int b)
    {
        await _send.SetBrightnessAsync("0xe4b323fffe9e2d38", b);
    }


    public async Task GetDeviceDetails(string address, string modelID)
    {
        var tcs = new TaskCompletionSource<bool>();

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
                    const int maxRetries = 5;
                    int retries = 0;
                    while (retries < maxRetries)
                    {
                        if (ep.Value.TryGetProperty("configured_reportings", out var reportings) &&
                            reportings.GetArrayLength() > 0)
                        {
                            foreach (var rep in reportings.EnumerateArray())
                            {
                                await _configuredReportings.NewConfigRepEntryAsync(
                                    "configuredreportings",
                                    address,
                                    modelID,
                                    rep.GetProperty("cluster").GetString(),
                                    rep.GetProperty("attribute").GetString(),
                                    rep.GetProperty("maximum_report_interval").GetInt32().ToString(),
                                    rep.GetProperty("minimum_report_interval").GetInt32().ToString(),
                                    rep.GetProperty("reportable_change").ToString(),
                                    ep.Name);

                                await _configuredReportings.NewConfigRepEntryAsync(
                                    "reporttemplate",
                                    address,
                                    modelID,
                                    rep.GetProperty("cluster").GetString(),
                                    rep.GetProperty("attribute").GetString(),
                                    rep.GetProperty("maximum_report_interval").GetInt32().ToString(),
                                    rep.GetProperty("minimum_report_interval").GetInt32().ToString(),
                                    rep.GetProperty("reportable_change").ToString(),
                                    ep.Name);
                            }

                            _conn.Client.ApplicationMessageReceivedAsync -= Handler;
                            tcs.TrySetResult(true);
                            return;
                        }

                        retries++;
                        await Task.Delay(5_000);
                        await _conn.Client.PublishAsync(
                            new MqttApplicationMessageBuilder()
                                .WithTopic("zigbee2mqtt/bridge/request/devices")
                                .WithPayload(JsonSerializer.Serialize(new { transaction = Guid.NewGuid().ToString() }))
                                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                                .Build());
                    }
                }
            }
        }

        _conn.Client.ApplicationMessageReceivedAsync += Handler;
        await _conn.Client.SubscribeAsync("zigbee2mqtt/bridge/devices");

        await _conn.Client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic("zigbee2mqtt/bridge/request/devices")
                .WithPayload(JsonSerializer.Serialize(new { transaction = Guid.NewGuid().ToString() }))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());

        await tcs.Task;
    }


    public async Task GetOptionDetails(string address,
        string model,
        List<string> readableProps,
        List<string> descriptions)
    {
        var tcs = new TaskCompletionSource<bool>();

        async Task Handler(MqttApplicationMessageReceivedEventArgs e)
        {
            if (e.ApplicationMessage.Topic != $"zigbee2mqtt/{address}")
                return;

            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            var node = JsonNode.Parse(payload)?.AsObject();
            if (node == null) return;

            for (int i = 0; i < readableProps.Count; i++)
            {
                var prop = readableProps[i];
                var desc = descriptions[i];
                if (node[prop] != null)
                {
                    await _option.SetOptionsAsync(address,  desc, node[prop]!.ToJsonString(), prop);
                    Console.WriteLine($"Option: {prop} = {node[prop]}");
                }
            }

            _conn.Client.ApplicationMessageReceivedAsync -= Handler;
            tcs.TrySetResult(true);
        }

        _conn.Client.ApplicationMessageReceivedAsync += Handler;

        await _conn.Client.PublishAsync(
            new MqttApplicationMessageBuilder()
                .WithTopic($"zigbee2mqtt/{address}")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(10_000));
        _conn.Client.ApplicationMessageReceivedAsync -= Handler;

        if (completed != tcs.Task)
            Console.WriteLine($"Timeout while getting options for {address}");
    }
}