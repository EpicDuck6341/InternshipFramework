using System.Text.Json.Nodes;
using MQTTnet;
using MQTTnet.Protocol;
using System.Text.Json;
using Elijah.Domain.Entities;
using Elijah.Domain.Models;
using Elijah.Logic.Abstract;

namespace Elijah.Logic.Concrete;

public class SendService(IMqttConnectionService mqtt) : ISendService //bump <-IMqttConnect
{
    public async Task SendReportConfigAsync(List<ReportConfig> configs)
    {
        foreach (var cfg in configs)
        {
            var payload = new
            {
                id = cfg.address,
                device = cfg.address,
                endpoint = cfg.endpoint,
                cluster = cfg.cluster,
                attribute = cfg.attribute,
                minimum_report_interval = cfg.minimum_report_interval,
                maximum_report_interval = cfg.maximum_report_interval,
                reportable_change = cfg.reportable_change,
            };

            var msg = new MqttApplicationMessageBuilder()
                .WithTopic("zigbee2mqtt/bridge/request/device/configure_reporting")
                .WithPayload(JsonSerializer.Serialize(payload))
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await mqtt.Client.PublishAsync(msg);
        }
    }

    public async Task SendDeviceOptionsAsync(List<ChangedOption> opts)
    {
        foreach (var opt in opts)
        {
            object value = opt.CurrentValue switch
            {
                string s when int.TryParse(s, out var i) => i,
                string s when double.TryParse(s, out var d) => d,
                _ => opt.CurrentValue,
            };

            var payload = new JsonObject { [opt.Property] = JsonValue.Create(value) };

            var msg = new MqttApplicationMessageBuilder()
                .WithTopic($"zigbee2mqtt/{opt.Address}/set")
                .WithPayload(payload.ToJsonString())
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await mqtt.Client.PublishAsync(msg);
        }
    }

    public async Task RemoveDeviceAsync(string address)
    {
        var payload = JsonSerializer.Serialize(
            new
            {
                id = address,
                force = true,
                block = false,
                transaction = Guid.NewGuid().ToString(),
            }
        );

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("zigbee2mqtt/bridge/request/device/remove")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await mqtt.Client.PublishAsync(msg);
    }

    //send the config to the ESP while it stays awake for a bit
    public async Task SetBrightnessAsync(string address, int brightness)
    {
        var payload = JsonSerializer.Serialize(new { brightness });

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic($"zigbee2mqtt/{address}/set")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await mqtt.Client.PublishAsync(msg);
    }

    public async Task PermitJoinAsync(int seconds)
    {
        var tx = Guid.NewGuid().ToString();
        var payload = JsonSerializer.Serialize(new { time = seconds, transaction = tx });

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("zigbee2mqtt/bridge/request/permit_join")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await mqtt.Client.PublishAsync(msg);
    }

    public async Task CloseJoinAsync()
    {
        var payload = JsonSerializer.Serialize(
            new { time = 0, transaction = Guid.NewGuid().ToString() }
        );
        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("zigbee2mqtt/bridge/request/permit_join")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await mqtt.Client.PublishAsync(msg);
    }
}
