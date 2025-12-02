using System.Text.Json.Nodes;
using MQTTnet;
using MQTTnet.Protocol;
using System.Text.Json;
using Elijah.Domain.Entities;
using Elijah.Domain.Models;
using Elijah.Logic.Abstract;

namespace Elijah.Logic.Concrete;

public class SendService(IMqttConnectionService mqtt) : ISendService
{
    // ------------------------------------------------------------ //
    // Sends all ReportConfig entries to Zigbee2MQTT                //
    // ------------------------------------------------------------ //
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

    // ------------------------------------------------------------ //
    // Sends changed device options converted to proper types       //
    // ------------------------------------------------------------ //
    public async Task SendDeviceOptionsAsync(List<ChangedOption> opts)
    {
        foreach (var opt in opts)
        {
            object? value = opt.CurrentValue switch
            {
                { } s when int.TryParse(s, out var i) => i,
                { } s when double.TryParse(s, out var d) => d,
                _ => opt.CurrentValue,
            };

            if (opt.Property != null)
            {
                var payload = new JsonObject
                {
                    [opt.Property] = JsonValue.Create(value)
                };

                var msg = new MqttApplicationMessageBuilder()
                    .WithTopic($"zigbee2mqtt/{opt.Address}/set")
                    .WithPayload(payload.ToJsonString())
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();

                await mqtt.Client.PublishAsync(msg);
            }
        }
    }

    // ----------------------------------------- //
    // Removes a device from the Zigbee network  //
    // ----------------------------------------- //
    public async Task RemoveDeviceAsync(string address)
    {
        var payload = JsonSerializer.Serialize(new
        {
            id = address,
            force = true,
            block = false,
            transaction = Guid.NewGuid().ToString(),
        });

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("zigbee2mqtt/bridge/request/device/remove")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await mqtt.Client.PublishAsync(msg);
    }
    
    // ------------------------------------------- //
    // Opens the Zigbee network for device joining //
    // ------------------------------------------- //
    public async Task PermitJoinAsync(int seconds)
    {
        var payload = JsonSerializer.Serialize(new
        {
            time = seconds,
            transaction = Guid.NewGuid().ToString(),
        });

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("zigbee2mqtt/bridge/request/permit_join")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await mqtt.Client.PublishAsync(msg);
    }

    // -------------------------------------- //
    // Closes the Zigbee network for joining  //
    // -------------------------------------- //
    public async Task CloseJoinAsync()
    {
        var payload = JsonSerializer.Serialize(new
        {
            time = 0,
            transaction = Guid.NewGuid().ToString(),
        });

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("zigbee2mqtt/bridge/request/permit_join")
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await mqtt.Client.PublishAsync(msg);
    }
}
