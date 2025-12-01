using Elijah.Domain.Models;
using Elijah.Logic.Abstract;
using Elijah.Logic.Concrete;
using Moq;

namespace Elijah.Test.Services;

public class ZigbeeClientTests
{
    private readonly Mock<IMqttConnectionService> _connMock;
    private readonly Mock<ISubscriptionService> _subMock;
    private readonly Mock<ISendService> _sendMock;
    private readonly Mock<IReceiveService> _recvMock;
    private readonly Mock<IDeviceService> _deviceMock;
    private readonly Mock<IConfiguredReportingsService> _reportingMock;
    private readonly Mock<IOptionService> _optionMock;
    private readonly Mock<IDeviceTemplateService> _templateMock;
    private readonly ZigbeeClient _sut;

    public ZigbeeClientTests()
    {
        _connMock = new Mock<IMqttConnectionService>();
        _subMock = new Mock<ISubscriptionService>();
        _sendMock = new Mock<ISendService>();
        _recvMock = new Mock<IReceiveService>();
        _deviceMock = new Mock<IDeviceService>();
        _reportingMock = new Mock<IConfiguredReportingsService>();
        _optionMock = new Mock<IOptionService>();
        _templateMock = new Mock<IDeviceTemplateService>();

        _sut = new ZigbeeClient(
            _connMock.Object,
            _subMock.Object,
            null!,
            _sendMock.Object,
            _recvMock.Object,
            _deviceMock.Object,
            _reportingMock.Object,
            _optionMock.Object,
            _templateMock.Object
        );
    }

    [Fact]
    public async Task ConnectToMqtt_CallsConnectAsync()
    {
        await _sut.ConnectToMqtt();

        _connMock.Verify(c => c.ConnectAsync(), Times.Once);
    }

    [Fact]
    public async Task SubscribeDevices_CallsServiceAndSetsReady()
    {
        await _sut.SubscribeDevices();

        _subMock.Verify(s => s.SubscribeExistingAsync(), Times.Once);
        Assert.True(_sut.IsReady);
    }

    [Fact]
    public async Task SendReportConfig_GetsChangedAndSends()
    {
        var addresses = new List<string> { "0x1234" };
        var configs = new List<ReportConfig> { new("0x1234", "c", "a", "60", "1", "1", "1") };

        _deviceMock.Setup(d => d.GetSubscribedAddressesAsync()).ReturnsAsync(addresses);
        _reportingMock.Setup(r => r.GetChangedReportConfigsAsync(addresses)).ReturnsAsync(configs);

        await _sut.SendReportConfig();

        _sendMock.Verify(s => s.SendReportConfigAsync(configs), Times.Once);
    }

    [Fact]
    public async Task SendDeviceOptions_GetsChangedAndSends()
    {
        var addresses = new List<string> { "0x1234" };
        var options = new List<ChangedOption>
        {
            new()
            {
                Address = "0x1234",
                Property = "temp",
                CurrentValue = "25",
            },
        };

        _deviceMock.Setup(d => d.GetSubscribedAddressesAsync()).ReturnsAsync(addresses);
        _optionMock.Setup(o => o.GetChangedOptionValuesAsync(addresses)).ReturnsAsync(options);

        await _sut.SendDeviceOptions();

        _sendMock.Verify(s => s.SendDeviceOptionsAsync(options), Times.Once);
    }

    [Fact]
    public async Task RemoveDevice_GetsAddressAndRemoves()
    {
        _deviceMock.Setup(d => d.GetDeviceByNameAsync("Sensor1")).ReturnsAsync("0x1234");
        _deviceMock
            .Setup(d => d.SetSubscribedStatusAsync(false, "0x1234"))
            .Returns(Task.CompletedTask);
        _sendMock.Setup(s => s.RemoveDeviceAsync("0x1234")).Returns(Task.CompletedTask);

        await _sut.RemoveDevice("Sensor1");

        _deviceMock.Verify(d => d.GetDeviceByNameAsync("Sensor1"), Times.Once);
        _deviceMock.Verify(d => d.SetSubscribedStatusAsync(false, "0x1234"), Times.Once);
        _sendMock.Verify(s => s.RemoveDeviceAsync("0x1234"), Times.Once);
    }

    [Fact]
    public void StartProcessingMessages_CallsService()
    {
        _sut.StartProcessingMessages();

        _recvMock.Verify(r => r.StartMessageLoop(), Times.Once);
    }
}
