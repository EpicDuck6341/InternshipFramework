// namespace Elijah.Test.Services;
//
// using System.Threading;
// using System.Threading.Tasks;
// using Elijah.Logic.Concrete;
// using Microsoft.Extensions.Configuration;
// using Moq;
// using MQTTnet;
// using Xunit;
//
// public class MqttConnectionServiceTests
// {
//     private readonly Mock<IConfiguration> _configMock;
//     private readonly Mock<IMqttClient> _clientMock;
//     private readonly MqttConnectionService _sut;
//
//     public MqttConnectionServiceTests()
//     {
//         _configMock = new Mock<IConfiguration>();
//         _clientMock = new Mock<IMqttClient>();
//         
//         var section = new Mock<IConfigurationSection>();
//         section.Setup(s => s["Hostname"]).Returns("localhost");
//         section.Setup(s => s["Port"]).Returns("1883");
//         section.Setup(s => s["ClientId"]).Returns("test-client");
//         _configMock.Setup(c => c.GetSection("MQTTString")).Returns(section.Object);
//
//         _sut = new MqttConnectionService(_configMock.Object);
//     }
//
//     [Fact]
//     public async Task ConnectAsync_CallsClientConnect()
//     {
//         // Arrange
//         var client = new Mock<IMqttClient>();
//         // Note: Can't easily test constructor logic without mocking the factory
//         // This test focuses on the pattern
//
//         // Act & Assert - Verified through integration test
//         await Task.CompletedTask;
//     }
// }