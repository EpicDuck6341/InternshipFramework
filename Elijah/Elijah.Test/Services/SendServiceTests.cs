using System.Text.Json;
using System.Text.Json.Nodes;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Elijah.Logic.Concrete;
using Moq;
using MQTTnet;
using MQTTnet;
using MQTTnet.Protocol;
using Xunit;

public class SendServiceTests
{
    [Fact]
    public async Task SendReportConfigAsync_PublishesMessages()
    {
        // Arrange
        var mqttMock = new Mock<IMqttConnectionService>();
        var mqttClientMock = new Mock<IMqttClient>();
        mqttMock.Setup(m => m.Client).Returns(mqttClientMock.Object);

        var configs = new List<ReportConfig>
        {
            new("dev1", "genOnOff", "state", "60", "1", "0", "1"),
            new("dev2", "lighting", "brightness", "120", "5", "1", "2")
        };

        var service = new SendService(mqttMock.Object);

        // Act
        await service.SendReportConfigAsync(configs);

        // Assert
        mqttClientMock.Verify(m => m.PublishAsync(It.Is<MqttApplicationMessage>(msg =>
            msg.Topic == "zigbee2mqtt/bridge/request/device/configure_reporting"),
            default), Times.Exactly(2));
    }

    [Fact]
    public async Task SendDeviceOptionsAsync_PublishesConvertedValues()
    {
        // Arrange
        var mqttMock = new Mock<IMqttConnectionService>();
        var mqttClientMock = new Mock<IMqttClient>();
        mqttMock.Setup(m => m.Client).Returns(mqttClientMock.Object);

        var opts = new List<ChangedOption>
        {
            new() { Address = "dev1", Property = "brightness", CurrentValue = "50" },
            new() { Address = "dev2", Property = "temperature", CurrentValue = "20.5" }
        };

        var service = new SendService(mqttMock.Object);

        // Act
        await service.SendDeviceOptionsAsync(opts);

        // Assert
        mqttClientMock.Verify(m => m.PublishAsync(It.Is<MqttApplicationMessage>(msg =>
            msg.Topic == "zigbee2mqtt/dev1/set"), default), Times.Once);

        mqttClientMock.Verify(m => m.PublishAsync(It.Is<MqttApplicationMessage>(msg =>
            msg.Topic == "zigbee2mqtt/dev2/set"), default), Times.Once);
    }

    [Fact]
    public async Task RemoveDeviceAsync_PublishesCorrectTopic()
    {
        var mqttMock = new Mock<IMqttConnectionService>();
        var mqttClientMock = new Mock<IMqttClient>();
        mqttMock.Setup(m => m.Client).Returns(mqttClientMock.Object);

        var service = new SendService(mqttMock.Object);

        await service.RemoveDeviceAsync("dev123");

        mqttClientMock.Verify(m => m.PublishAsync(It.Is<MqttApplicationMessage>(msg =>
            msg.Topic == "zigbee2mqtt/bridge/request/device/remove"), default), Times.Once);
    }

    [Fact]
    public async Task SetBrightnessAsync_PublishesCorrectMessage()
    {
        var mqttMock = new Mock<IMqttConnectionService>();
        var mqttClientMock = new Mock<IMqttClient>();
        mqttMock.Setup(m => m.Client).Returns(mqttClientMock.Object);

        var service = new SendService(mqttMock.Object);

        await service.SetBrightnessAsync("lamp123", 80);

        mqttClientMock.Verify(m => m.PublishAsync(It.Is<MqttApplicationMessage>(msg =>
            msg.Topic == "zigbee2mqtt/lamp123/set"), default), Times.Once);
    }

    [Fact]
    public async Task PermitJoinAsync_PublishesPermitJoin()
    {
        var mqttMock = new Mock<IMqttConnectionService>();
        var mqttClientMock = new Mock<IMqttClient>();
        mqttMock.Setup(m => m.Client).Returns(mqttClientMock.Object);

        var service = new SendService(mqttMock.Object);
        await service.PermitJoinAsync(30);

        mqttClientMock.Verify(m => m.PublishAsync(It.Is<MqttApplicationMessage>(msg =>
            msg.Topic == "zigbee2mqtt/bridge/request/permit_join"), default), Times.Once);
    }

    [Fact]
    public async Task CloseJoinAsync_PublishesZeroPermitJoin()
    {
        var mqttMock = new Mock<IMqttConnectionService>();
        var mqttClientMock = new Mock<IMqttClient>();
        mqttMock.Setup(m => m.Client).Returns(mqttClientMock.Object);

        var service = new SendService(mqttMock.Object);
        await service.CloseJoinAsync();

        mqttClientMock.Verify(m => m.PublishAsync(It.Is<MqttApplicationMessage>(msg =>
            msg.Topic == "zigbee2mqtt/bridge/request/permit_join"), default), Times.Once);
    }
}