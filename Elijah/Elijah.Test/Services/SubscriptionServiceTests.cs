// using System.Collections.Generic;
// using System.Threading;
// using System.Threading.Tasks;
// using Elijah.Logic.Abstract;
// using Elijah.Logic.Concrete;
// using MQTTnet;
// using Moq;
// using Xunit;
//
// namespace Elijah.Logic.Tests.Services;
//
// public class SubscriptionServiceTests
// {
//     private readonly Mock<IMqttConnectionService> _mqttMock;
//     private readonly Mock<IMqttClient> _clientMock;
//     private readonly Mock<IDeviceService> _deviceMock;
//     private readonly SubscriptionService _sut;
//
//     public SubscriptionServiceTests()
//     {
//         _mqttMock = new Mock<IMqttConnectionService>();
//         _clientMock = new Mock<IMqttClient>();
//         _deviceMock = new Mock<IDeviceService>();
//         _mqttMock.Setup(m => m.Client).Returns(_clientMock.Object);
//         _sut = new SubscriptionService(_mqttMock.Object, _deviceMock.Object);
//     }
//
//     [Fact]
//     public async Task SubscribeExistingAsync_SubscribesToAllUnsubscribed()
//     {
//         // Arrange
//         var unsubscribed = new List<string> { "0x1234", "0x5678" };
//         _deviceMock.Setup(d => d.GetUnsubscribedAddressesAsync()).ReturnsAsync(unsubscribed);
//         _clientMock.Setup(c => c.SubscribeAsync(It.IsAny<MqttTopicFilter[]>(), It.IsAny<CancellationToken>()))
//             .ReturnsAsync(new List<MqttClientSubscribeResult>());
//
//         // Act
//         await _sut.SubscribeExistingAsync();
//
//         // Assert
//         _clientMock.Verify(c => c.SubscribeAsync(
//             It.Is<MqttTopicFilter[]>(f => f.Length == 2), It.IsAny<CancellationToken>()), Times.Once);
//         _deviceMock.Verify(d => d.SetSubscribedStatusAsync(true, "0x1234"), Times.Once);
//         _deviceMock.Verify(d => d.SetSubscribedStatusAsync(true, "0x5678"), Times.Once);
//     }
//
//     [Fact]
//     public async Task SubscribeAsync_SubscribesAndUpdatesStatus()
//     {
//         // Arrange
//         _clientMock.Setup(c => c.SubscribeAsync(It.IsAny<MqttTopicFilter[]>(), It.IsAny<CancellationToken>()))
//             .ReturnsAsync(new List<MqttClientSubscribeResult>());
//
//         // Act
//         await _sut.SubscribeAsync("0x1234");
//
//         // Assert
//         _clientMock.Verify(c => c.SubscribeAsync(
//             It.Is<MqttTopicFilter[]>(f => f[0].Topic == "zigbee2mqtt/0x1234"), It.IsAny<CancellationToken>()), Times.Once);
//         _deviceMock.Verify(d => d.SetSubscribedStatusAsync(true, "0x1234"), Times.Once);
//     }
// }