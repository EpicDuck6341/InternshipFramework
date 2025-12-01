using System.Text;
using Elijah.Logic.Abstract;
using Elijah.Logic.Concrete;
using Moq;
using MQTTnet;
using MQTTnet.Packets;

namespace Elijah.Test.Services;

public class ReceiveServiceTests
{
    private static MqttApplicationMessageReceivedEventArgs BuildArgs(string topic, string payload)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .Build();

        return new MqttApplicationMessageReceivedEventArgs(
            clientId: "test-client",
            applicationMessage: message,
            publishPacket: new MqttPublishPacket(),
            acknowledgeHandler: (args, token) => Task.CompletedTask
        );
    }

    [Fact]
    public async Task OnMessageAsync_IgnoresBridgeTopics()
    {
        var mqtt = new Mock<IMqttConnectionService>();
        var client = new Mock<IMqttClient>();
        mqtt.Setup(m => m.Client).Returns(client.Object);

        var devices = new Mock<IDeviceService>();
        var filters = new Mock<IDeviceFilterService>();

        var service = new ReceiveService(mqtt.Object, devices.Object, filters.Object);

        var args = BuildArgs("zigbee2mqtt/bridge/state", "{\"on\":true}");

        var method = typeof(ReceiveService).GetMethod(
            "OnMessageAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        await (Task)method.Invoke(service, new object[] { args });

        devices.Verify(d => d.QueryModelIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task OnMessageAsync_FiltersCorrectKeys_AndWritesToConsole()
    {
        var mqtt = new Mock<IMqttConnectionService>();
        var client = new Mock<IMqttClient>();
        mqtt.Setup(m => m.Client).Returns(client.Object);

        var devices = new Mock<IDeviceService>();
        var filters = new Mock<IDeviceFilterService>();

        devices.Setup(d => d.QueryModelIdAsync("lamp1")).ReturnsAsync("model123");
        devices.Setup(d => d.QueryDeviceNameAsync("lamp1")).ReturnsAsync("Lamp Livingroom");

        filters
            .Setup(f => f.QueryDataFilterAsync("model123"))
            .ReturnsAsync(new List<string> { "state", "brightness" });

        var service = new ReceiveService(mqtt.Object, devices.Object, filters.Object);

        var payload = "{\"state\":\"ON\",\"brightness\":150,\"ignored\":true}";
        var args = BuildArgs("zigbee2mqtt/lamp1", payload);

        var sb = new StringBuilder();
        Console.SetOut(new System.IO.StringWriter(sb));

        var method = typeof(ReceiveService).GetMethod(
            "OnMessageAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        await (Task)method.Invoke(service, new object[] { args });

        var output = sb.ToString();

        Assert.Contains("state", output);
        Assert.Contains("brightness", output);
        Assert.DoesNotContain("ignored", output);

        devices.Verify(d => d.QueryModelIdAsync("lamp1"), Times.Once);
        devices.Verify(d => d.QueryDeviceNameAsync("lamp1"), Times.Once);
        filters.Verify(f => f.QueryDataFilterAsync("model123"), Times.Once);
    }
}
