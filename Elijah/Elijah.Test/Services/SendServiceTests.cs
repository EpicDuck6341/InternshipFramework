// using System.Collections.Generic;
// using System.Text.Json;
// using System.Threading;
// using System.Threading.Tasks;
// using Elijah.Domain.Entities;
// using Elijah.Logic.Abstract;
// using Elijah.Logic.Concrete;
// using MQTTnet;
// using Moq;
// using Xunit;
//
// namespace Elijah.Logic.Tests.Services;
//
// public class SendServiceTests
// {
//     private readonly Mock<IMqttConnectionService> _mqttMock;
//     private readonly Mock<IMqttClient> _clientMock;
//     private readonly SendService _sut;
//
//     public SendServiceTests()
//     {
//         _mqttMock = new Mock<IMqttConnectionService>();
//         _clientMock = new Mock<IMqttClient>();
//         _mqttMock.Setup(m => m.Client).Returns(_clientMock.Object);
//         _sut = new SendService(_mqttMock.Object);
//     }
//
//     [Fact]
//     public async Task SendReportConfigAsync_PublishesCorrectMessages()
//     {
//         // Arrange
//         var configs = new List<ReportConfig>
//         {
//             new("0x1234", "0x0006", "onOff", "60", "1", "1", "1")
//         };
//
//         MqttApplicationMessage captured = null!;
//         _clientMock.Setup(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
//             .Callback<MqttApplicationMessage, CancellationToken>((msg, _) => captured = msg)
//             .Returns(Task.CompletedTask);
//
//         // Act
//         await _sut.SendReportConfigAsync(configs);
//
//         // Assert
//         Assert.NotNull(captured);
//         Assert.Equal("zigbee2mqtt/bridge/request/device/configure_reporting", captured.Topic);
//         var payload = JsonSerializer.Deserialize<JsonElement>(captured.Payload);
//         Assert.Equal("0x1234", payload.GetProperty("id").GetString());
//         Assert.Equal("0x0006", payload.GetProperty("cluster").GetString());
//     }
//
//     [Fact]
//     public async Task SendDeviceOptionsAsync_ConvertsNumericValues()
//     {
//         // Arrange
//         var options = new List<ChangedOption>
//         {
//             new() { Address = "0x1234", Property = "brightness", CurrentValue = "128" },
//             new() { Address = "0x5678", Property = "color", CurrentValue = "red" }
//         };
//
//         var capturedMessages = new List<MqttApplicationMessage>();
//         _clientMock.Setup(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
//             .Callback<MqttApplicationMessage, CancellationToken>((msg, _) => capturedMessages.Add(msg))
//             .Returns(Task.CompletedTask);
//
//         // Act
//         await _sut.SendDeviceOptionsAsync(options);
//
//         // Assert
//         Assert.Equal(2, capturedMessages.Count);
//         
//         var brightnessPayload = JsonSerializer.Deserialize<JsonElement>(capturedMessages[0].Payload);
//         Assert.Equal(128, brightnessPayload.GetProperty("brightness").GetInt32());
//         
//         var colorPayload = JsonSerializer.Deserialize<JsonElement>(capturedMessages[1].Payload);
//         Assert.Equal("red", colorPayload.GetProperty("color").GetString());
//     }
//
//     [Fact]
//     public async Task RemoveDeviceAsync_PublishesCorrectTopic()
//     {
//         // Arrange
//         MqttApplicationMessage captured = null!;
//         _clientMock.Setup(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
//             .Callback<MqttApplicationMessage, CancellationToken>((msg, _) => captured = msg)
//             .Returns(Task.CompletedTask);
//
//         // Act
//         await _sut.RemoveDeviceAsync("0x1234");
//
//         // Assert
//         Assert.NotNull(captured);
//         Assert.Equal("zigbee2mqtt/bridge/request/device/remove", captured.Topic);
//     }
//
//     [Fact]
//     public async Task SetBrightnessAsync_PublishesCorrectPayload()
//     {
//         // Arrange
//         MqttApplicationMessage captured = null!;
//         _clientMock.Setup(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
//             .Callback<MqttApplicationMessage, CancellationToken>((msg, _) => captured = msg)
//             .Returns(Task.CompletedTask);
//
//         // Act
//         await _sut.SetBrightnessAsync("0x1234", 200);
//
//         // Assert
//         Assert.NotNull(captured);
//         Assert.Equal("zigbee2mqtt/0x1234/set", captured.Topic);
//         var payload = JsonSerializer.Deserialize<JsonElement>(captured.Payload);
//         Assert.Equal(200, payload.GetProperty("brightness").GetInt32());
//     }
//
//     [Fact]
//     public async Task PermitJoinAsync_PublishesWithTimeout()
//     {
//         // Arrange
//         MqttApplicationMessage captured = null!;
//         _clientMock.Setup(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
//             .Callback<MqttApplicationMessage, CancellationToken>((msg, _) => captured = msg)
//             .Returns(Task.CompletedTask);
//
//         // Act
//         await _sut.PermitJoinAsync(60);
//
//         // Assert
//         Assert.NotNull(captured);
//         Assert.Equal("zigbee2mqtt/bridge/request/permit_join", captured.Topic);
//         var payload = JsonSerializer.Deserialize<JsonElement>(captured.Payload);
//         Assert.Equal(60, payload.GetProperty("time").GetInt32());
//     }
//
//     [Fact]
//     public async Task CloseJoinAsync_PublishesTimeoutZero()
//     {
//         // Arrange
//         MqttApplicationMessage captured = null!;
//         _clientMock.Setup(c => c.PublishAsync(It.IsAny<MqttApplicationMessage>(), It.IsAny<CancellationToken>()))
//             .Callback<MqttApplicationMessage, CancellationToken>((msg, _) => captured = msg)
//             .Returns(Task.CompletedTask);
//
//         // Act
//         await _sut.CloseJoinAsync();
//
//         // Assert
//         Assert.NotNull(captured);
//         var payload = JsonSerializer.Deserialize<JsonElement>(captured.Payload);
//         Assert.Equal(0, payload.GetProperty("time").GetInt32());
//     }
// }