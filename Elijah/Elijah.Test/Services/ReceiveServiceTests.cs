// using System.Text;
// using System.Text.Json.Nodes;
// using System.Threading.Tasks;
// using Elijah.Logic.Abstract;
// using Elijah.Logic.Concrete;
// using MQTTnet;
// using Moq;
// using Xunit;
//
// namespace Elijah.Logic.Tests.Services;
//
// public class ReceiveServiceTests
// {
//     private readonly Mock<IMqttConnectionService> _mqttMock;
//     private readonly Mock<IDeviceService> _deviceMock;
//     private readonly Mock<IDeviceFilterService> _filterMock;
//     private readonly ReceiveService _sut;
//     private readonly Mock<IMqttClient> _clientMock;
//
//     public ReceiveServiceTests()
//     {
//         _mqttMock = new Mock<IMqttConnectionService>();
//         _deviceMock = new Mock<IDeviceService>();
//         _filterMock = new Mock<IDeviceFilterService>();
//         _sut = new ReceiveService(_mqttMock.Object, _deviceMock.Object, _filterMock.Object);
//         _clientMock = new Mock<IMqttClient>();
//         _mqttMock.Setup(m => m.Client).Returns(_clientMock.Object);
//     }
//
//     [Fact]
//     public void StartMessageLoop_AttachesHandler()
//     {
//         // Act
//         _sut.StartMessageLoop();
//
//         // Assert
//         _clientMock.VerifySet(c => c.ApplicationMessageReceivedAsync = It.IsAny<Func<MqttApplicationMessageReceivedEventArgs, Task>>());
//     }
//
//     [Fact]
//     public async Task OnMessageAsync_BridgeMessage_Ignores()
//     {
//         // Arrange
//         _sut.StartMessageLoop();
//         
//         var message = new MqttApplicationMessage 
//         { 
//             Topic = "zigbee2mqtt/bridge/devices",
//             Payload = Encoding.UTF8.GetBytes("{}")
//         };
//         var args = new MqttApplicationMessageReceivedEventArgs("test", message, null);
//
//         // Act
//         var handler = _clientMock.Invocations[0].Arguments[0] as Func<MqttApplicationMessageReceivedEventArgs, Task>;
//         await handler(args);
//
//         // Assert
//         _deviceMock.Verify(d => d.QueryModelIDAsync(It.IsAny<string>()), Times.Never);
//         _filterMock.Verify(f => f.QueryDataFilterAsync(It.IsAny<string>()), Times.Never);
//     }
// }