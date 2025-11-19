using System.Collections.Generic;
using System.Threading.Tasks;
using Elijah.Logic.Abstract;
using Elijah.Logic.Concrete;
using MQTTnet;
using Moq;
using Xunit;

namespace Elijah.Test.Services;

public class SubscriptionServiceTests
{
    private readonly Mock<IMqttConnectionService> _mqttMock;
    private readonly Mock<IMqttClient> _mqttClientMock;
    private readonly Mock<IDeviceService> _deviceServiceMock;
    private readonly SubscriptionService _sut;

    public SubscriptionServiceTests()
    {
        _mqttMock = new Mock<IMqttConnectionService>();
        _mqttClientMock = new Mock<IMqttClient>();
        _deviceServiceMock = new Mock<IDeviceService>();

        _mqttMock.Setup(m => m.Client).Returns(_mqttClientMock.Object);
        _sut = new SubscriptionService(_mqttMock.Object, _deviceServiceMock.Object);
    }

    [Fact]
    public async Task SubscribeExistingAsync_SubscribesToAllUnsubscribed()
    {
        var unsubscribed = new List<string> { "device1", "device2" };
        _deviceServiceMock.Setup(d => d.GetUnsubscribedAddressesAsync())
            .ReturnsAsync(unsubscribed);

        _deviceServiceMock.Setup(d => d.SetSubscribedStatusAsync(It.IsAny<bool>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);


        await _sut.SubscribeExistingAsync();


        _deviceServiceMock.Verify(d => d.SetSubscribedStatusAsync(true, "device1"), Times.Once);
        _deviceServiceMock.Verify(d => d.SetSubscribedStatusAsync(true, "device2"), Times.Once);
    }

    [Fact]
    public async Task SubscribeAsync_SingleDevice_WorksCorrectly()
    {
        _deviceServiceMock.Setup(d => d.SetSubscribedStatusAsync(true, "abc123"))
            .Returns(Task.CompletedTask);


        await _sut.SubscribeAsync("abc123");


        _deviceServiceMock.Verify(d => d.SetSubscribedStatusAsync(true, "abc123"), Times.Once);
    }
}