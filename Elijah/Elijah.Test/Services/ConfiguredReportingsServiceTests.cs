using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Elijah.Logic.Concrete;
using MockQueryable.Moq;
using Moq;

namespace Elijah.Test.Services;

public class ConfiguredReportingsServiceTests
{
    private readonly Mock<IZigbeeRepository> _repoMock;
    private readonly Mock<IDeviceService> _deviceServiceMock;
    private readonly ConfiguredReportingsService _sut;

    public ConfiguredReportingsServiceTests()
    {
        _repoMock = new Mock<IZigbeeRepository>();
        _deviceServiceMock = new Mock<IDeviceService>();
        _sut = new ConfiguredReportingsService(_repoMock.Object, _deviceServiceMock.Object);
    }

    [Fact]
    public async Task QueryReportIntervalAsync_DeviceNotFound_ReturnsEmptyList()
    {
        _deviceServiceMock
            .Setup(d => d.GetDeviceByAdressAsync("nonexistent"))
            .ReturnsAsync((int?)null);

        var result = await _sut.QueryReportIntervalAsync("nonexistent");

        Assert.Empty(result);
        _deviceServiceMock.Verify(d => d.GetDeviceByAdressAsync("nonexistent"), Times.Once);
    }

    [Fact]
    public async Task QueryReportIntervalAsync_DeviceFound_ReturnsMappedConfigs()
    {
        var deviceId = 42;
        _deviceServiceMock.Setup(d => d.GetDeviceByAdressAsync("0x1234")).ReturnsAsync(deviceId);

        var configs = new List<ConfiguredReporting>
        {
            new()
            {
                DeviceId = 42,
                Cluster = "0x0006",
                Attribute = "onOff",
                MaximumReportInterval = "60",
                MinimumReportInterval = "1",
                ReportableChange = "1",
                Endpoint = "1",
            },
        };

        var mockSet = configs.AsQueryable().BuildMockDbSet();
        _repoMock.Setup(r => r.Query<ConfiguredReporting>()).Returns(mockSet.Object);

        var result = await _sut.QueryReportIntervalAsync("0x1234");

        Assert.Single(result);
        Assert.Equal("0x1234", result[0].address);
        Assert.Equal("0x0006", result[0].cluster);
    }

    [Fact]
    public async Task NewConfigRepEntryAsync_DeviceNotFound_ThrowsException()
    {
        _deviceServiceMock
            .Setup(d => d.GetDeviceByAdressAsync("nonexistent"))
            .ReturnsAsync(null as int?);

        await Assert.ThrowsAsync<Exception>(
            () => _sut.NewConfigRepEntryAsync("nonexistent", "c", "a", "60", "1", "1", "1")
        );
    }

    [Fact]
    public async Task NewConfigRepEntryAsync_ValidData_CreatesEntry()
    {
        _deviceServiceMock.Setup(d => d.GetDeviceByAdressAsync("0x1234")).ReturnsAsync(42);

        ConfiguredReporting captured = null!;
        _repoMock
            .Setup(r =>
                r.CreateAsync(It.IsAny<ConfiguredReporting>(), It.IsAny<bool>(), true, default)
            )
            .Callback<ConfiguredReporting, bool, bool, System.Threading.CancellationToken>(
                (cr, _, _, _) => captured = cr
            )
            .Returns(Task.CompletedTask);

        _repoMock.Setup(r => r.SaveChangesAsync(true, default)).Returns(Task.FromResult(1));

        await _sut.NewConfigRepEntryAsync("0x1234", "0x0006", "onOff", "60", "1", "1", "1");

        Assert.NotNull(captured);
        Assert.Equal(42, captured.DeviceId);
        Assert.Equal("0x0006", captured.Cluster);
        Assert.True(captured.IsTemplate);
        Assert.False(captured.Changed);
        _repoMock.Verify(r => r.SaveChangesAsync(true, default), Times.Once);
    }

    [Fact]
    public async Task AdjustRepConfigAsync_DeviceNotFound_ReturnsWithoutAction()
    {
        _deviceServiceMock
            .Setup(d => d.GetDeviceByAdressAsync("nonexistent"))
            .ReturnsAsync(null as int?);

        await _sut.AdjustRepConfigAsync("nonexistent", "c", "a", "60", "1", "1", "1");

        _repoMock.Verify(r => r.Query<ConfiguredReporting>(), Times.Never);
    }

    [Fact]
    public async Task AdjustRepConfigAsync_ConfigExists_UpdatesAndMarksChanged()
    {
        var deviceId = 42;
        _deviceServiceMock.Setup(d => d.GetDeviceByAdressAsync("0x1234")).ReturnsAsync(deviceId);

        var existing = new ConfiguredReporting
        {
            DeviceId = 42,
            Cluster = "0x0006",
            Attribute = "onOff",
            Endpoint = "1",
            MaximumReportInterval = "30",
            MinimumReportInterval = "5",
            ReportableChange = "0",
        };

        var mockSet = new List<ConfiguredReporting> { existing }
            .AsQueryable()
            .BuildMockDbSet();
        _repoMock.Setup(r => r.Query<ConfiguredReporting>()).Returns(mockSet.Object);
        _repoMock.Setup(r => r.SaveChangesAsync(true, default)).Returns(Task.FromResult(1));

        await _sut.AdjustRepConfigAsync("0x1234", "0x0006", "onOff", "60", "1", "1", "1");

        Assert.Equal("60", existing.MaximumReportInterval);
        Assert.Equal("1", existing.MinimumReportInterval);
        Assert.Equal("1", existing.ReportableChange);
        Assert.True(existing.Changed);
        _repoMock.Verify(r => r.SaveChangesAsync(true, default), Times.Once);
    }

    [Fact]
    public async Task GetChangedReportConfigsAsync_NoSubscribedAddresses_ReturnsEmptyList()
    {
        var result = await _sut.GetChangedReportConfigsAsync(new List<string>());

        Assert.Empty(result);
        _repoMock.Verify(r => r.Query<ConfiguredReporting>(), Times.Never);
    }

    [Fact]
    public async Task GetChangedReportConfigsAsync_ChangedConfigsExist_ReturnsAndResets()
    {
        var subscribed = new List<string> { "0x1234" };
        var device = new Device { Id = 42, Address = "0x1234" };

        var changed = new List<ConfiguredReporting>
        {
            new()
            {
                DeviceId = 42,
                Device = device,
                Changed = true,
                Cluster = "0x0006",
                Attribute = "onOff",
                Endpoint = "1",
                MaximumReportInterval = "60",
                MinimumReportInterval = "1",
                ReportableChange = "1",
            },
        };

        var mockSet = changed.AsQueryable().BuildMockDbSet();
        _repoMock.Setup(r => r.Query<ConfiguredReporting>()).Returns(mockSet.Object);
        _repoMock.Setup(r => r.SaveChangesAsync(true, default)).Returns(Task.FromResult(1));

        var result = await _sut.GetChangedReportConfigsAsync(subscribed);

        Assert.Single(result);
        Assert.Equal("0x1234", result[0].address);
        Assert.False(changed.First().Changed);
        _repoMock.Verify(r => r.SaveChangesAsync(true, default), Times.Once);
    }
}
