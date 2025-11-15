using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MockQueryable.Moq;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Elijah.Logic.Concrete;
using Moq;
using Xunit;

namespace Elijah.Logic.Tests.Services;

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
        // Arrange
        _deviceServiceMock
            .Setup(d => d.AddressToIdAsync("nonexistent"))
            .ReturnsAsync((int?)null);


        // Act
        var result = await _sut.QueryReportIntervalAsync("nonexistent");

        // Assert
        Assert.Empty(result);
        _deviceServiceMock.Verify(d => d.AddressToIdAsync("nonexistent"), Times.Once);
    }

    [Fact]
    public async Task QueryReportIntervalAsync_DeviceFound_ReturnsMappedConfigs()
    {
        // Arrange
        var deviceId = 42;
        _deviceServiceMock.Setup(d => d.AddressToIdAsync("0x1234"))
            .ReturnsAsync(deviceId);

        var configs = new List<ConfiguredReporting>
        {
            new() { 
                DeviceId = 42, Cluster = "0x0006", Attribute = "onOff", 
                MaximumReportInterval = "60", MinimumReportInterval = "1", 
                ReportableChange = "1", Endpoint = "1" 
            }
        };

        var mockSet = configs.AsQueryable().BuildMockDbSet();
        _repoMock.Setup(r => r.Query<ConfiguredReporting>()).Returns(mockSet.Object);

        // Act
        var result = await _sut.QueryReportIntervalAsync("0x1234");

        // Assert
        Assert.Single(result);
        Assert.Equal("0x1234", result[0].address);
        Assert.Equal("0x0006", result[0].cluster);
    }

    [Fact]
    public async Task NewConfigRepEntryAsync_DeviceNotFound_ThrowsException()
    {
        // Arrange
        _deviceServiceMock.Setup(d => d.AddressToIdAsync("nonexistent"))
            .ReturnsAsync(null as int?);


        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => 
            _sut.NewConfigRepEntryAsync("nonexistent", "c", "a", "60", "1", "1", "1"));
    }

    [Fact]
    public async Task NewConfigRepEntryAsync_ValidData_CreatesEntry()
    {
        // Arrange
        _deviceServiceMock.Setup(d => d.AddressToIdAsync("0x1234"))
            .ReturnsAsync(42);

        ConfiguredReporting captured = null!;
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<ConfiguredReporting>(), It.IsAny<bool>(), true, default))
            .Callback<ConfiguredReporting, bool, bool, System.Threading.CancellationToken>((cr, _, _, _) => captured = cr)
            .Returns(Task.CompletedTask);

        _repoMock.Setup(r => r.SaveChangesAsync(true, default)).Returns(Task.FromResult(1));

        // Act
        await _sut.NewConfigRepEntryAsync("0x1234", "0x0006", "onOff", "60", "1", "1", "1");

        // Assert
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
        // Arrange
        _deviceServiceMock.Setup(d => d.AddressToIdAsync("nonexistent"))
            .ReturnsAsync(null as int?);


        // Act
        await _sut.AdjustRepConfigAsync("nonexistent", "c", "a", "60", "1", "1", "1");

        // Assert
        _repoMock.Verify(r => r.Query<ConfiguredReporting>(), Times.Never);
    }

    [Fact]
    public async Task AdjustRepConfigAsync_ConfigExists_UpdatesAndMarksChanged()
    {
        // Arrange
        var deviceId = 42;
        _deviceServiceMock.Setup(d => d.AddressToIdAsync("0x1234"))
            .ReturnsAsync(deviceId);

        var existing = new ConfiguredReporting 
        { 
            DeviceId = 42, Cluster = "0x0006", Attribute = "onOff", Endpoint = "1",
            MaximumReportInterval = "30", MinimumReportInterval = "5", ReportableChange = "0"
        };

        var mockSet = new List<ConfiguredReporting> { existing }.AsQueryable().BuildMockDbSet();
        _repoMock.Setup(r => r.Query<ConfiguredReporting>()).Returns(mockSet.Object);
        _repoMock.Setup(r => r.SaveChangesAsync(true, default)).Returns(Task.FromResult(1));

        // Act
        await _sut.AdjustRepConfigAsync("0x1234", "0x0006", "onOff", "60", "1", "1", "1");

        // Assert
        Assert.Equal("60", existing.MaximumReportInterval);
        Assert.Equal("1", existing.MinimumReportInterval);
        Assert.Equal("1", existing.ReportableChange);
        Assert.True(existing.Changed);
        _repoMock.Verify(r => r.SaveChangesAsync(true, default), Times.Once);
    }

    [Fact]
    public async Task GetChangedReportConfigsAsync_NoSubscribedAddresses_ReturnsEmptyList()
    {
        // Act
        var result = await _sut.GetChangedReportConfigsAsync(new List<string>());

        // Assert
        Assert.Empty(result);
        _repoMock.Verify(r => r.Query<ConfiguredReporting>(), Times.Never);
    }

    [Fact]
    public async Task GetChangedReportConfigsAsync_ChangedConfigsExist_ReturnsAndResets()
    {
        // Arrange
        var subscribed = new List<string> { "0x1234" };
        var device = new Device { Id = 42, Address = "0x1234" };
        
        var changed = new List<ConfiguredReporting>
        {
            new() { 
                DeviceId = 42, Device = device, Changed = true,
                Cluster = "0x0006", Attribute = "onOff", Endpoint = "1",
                MaximumReportInterval = "60", MinimumReportInterval = "1", ReportableChange = "1"
            }
        };

        var mockSet = changed.AsQueryable().BuildMockDbSet();
        _repoMock.Setup(r => r.Query<ConfiguredReporting>()).Returns(mockSet.Object);
        _repoMock.Setup(r => r.SaveChangesAsync(true, default)).Returns(Task.FromResult(1));

        // Act
        var result = await _sut.GetChangedReportConfigsAsync(subscribed);

        // Assert
        Assert.Single(result);
        Assert.Equal("0x1234", result[0].address);
        Assert.False(changed.First().Changed);
        _repoMock.Verify(r => r.SaveChangesAsync(true, default), Times.Once);
    }
}