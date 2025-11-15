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

public class DeviceServiceTests
{
    private readonly Mock<IZigbeeRepository> _repoMock;
    private readonly Mock<IDeviceTemplateService> _templateServiceMock;
    private readonly DeviceService _sut;

    public DeviceServiceTests()
    {
        _repoMock = new Mock<IZigbeeRepository>();
        _templateServiceMock = new Mock<IDeviceTemplateService>();
        _sut = new DeviceService(_repoMock.Object, _templateServiceMock.Object);
    }

    [Fact]
    public async Task AddressToIdAsync_ReturnsCorrectId()
    {
        // Arrange
        var device = new Device { Id = 42, Address = "0x1234" };
        var mockSet = new List<Device> { device }.AsQueryable().BuildMockDbSet();
        _repoMock.Setup(r => r.Query<Device>()).Returns(mockSet.Object);

        // Act
        var result = await _sut.AddressToIdAsync("0x1234");

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task QueryDeviceNameAsync_ReturnsName()
    {
        // Arrange
        var device = new Device { Address = "0x1234", Name = "Sensor1" };
        var mockSet = new List<Device> { device }.AsQueryable().BuildMockDbSet();
        _repoMock.Setup(r => r.Query<Device>()).Returns(mockSet.Object);

        // Act
        var result = await _sut.QueryDeviceNameAsync("0x1234");

        // Assert
        Assert.Equal("Sensor1", result);
    }

    [Fact]
    public async Task SetSubscribedStatusAsync_UpdatesStatus()
    {
        // Arrange
        var device = new Device { Address = "0x1234", Subscribed = false };
        var mockSet = new List<Device> { device }.AsQueryable().BuildMockDbSet();
        _repoMock.Setup(r => r.Query<Device>()).Returns(mockSet.Object);
        _repoMock.Setup(r => r.SaveChangesAsync(true, default)).Returns(Task.FromResult(1));

        // Act
        await _sut.SetSubscribedStatusAsync(true, "0x1234");

        // Assert
        Assert.True(device.Subscribed);
        _repoMock.Verify(r => r.SaveChangesAsync(true, default), Times.Once);
    }

    [Fact]
    public async Task DevicePresentAsync_DeviceExists_ReturnsTrue()
    {
        // Arrange
        var devices = new List<Device> { new() { Address = "0x1234" } }.AsQueryable();
        var mockSet = devices.BuildMockDbSet();
        _repoMock.Setup(r => r.Query<Device>()).Returns(mockSet.Object);

        // Act
        var result = await _sut.DevicePresentAsync("model123", "0x1234");

        // Assert
        Assert.True(result);
        _templateServiceMock.Verify(t => t.ModelPresentAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DevicePresentAsync_DeviceNotExist_CallsTemplateService()
    {
        // Arrange
        var devices = Enumerable.Empty<Device>().AsQueryable();
        var mockSet = devices.BuildMockDbSet();
        _repoMock.Setup(r => r.Query<Device>()).Returns(mockSet.Object);
        _templateServiceMock.Setup(t => t.ModelPresentAsync("model123")).ReturnsAsync(false);

        // Act
        var result = await _sut.DevicePresentAsync("model123", "0x1234");

        // Assert
        Assert.False(result);
        _templateServiceMock.Verify(t => t.ModelPresentAsync("model123"), Times.Once);
    }

    [Fact]
    public async Task NewDeviceEntryAsync_ValidData_CreatesDevice()
    {
        // Arrange
        var template = new DeviceTemplate { Id = 10, ModelId = "model123" };
        var templates = new List<DeviceTemplate> { template }.AsQueryable();
        _repoMock.Setup(r => r.Query<DeviceTemplate>()).Returns(templates.BuildMockDbSet().Object);

        Device captured = null!;
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<Device>(), It.IsAny<bool>(), true, default))
            .Callback<Device, bool, bool, System.Threading.CancellationToken>((d, _, _, _) => captured = d)
            .Returns(Task.CompletedTask);

        _repoMock.Setup(r => r.SaveChangesAsync(true, default)).Returns(Task.FromResult(1));

        // Act
        await _sut.NewDeviceEntryAsync("model123", "NewSensor", "0x1234");

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(10, captured.TemplateId);
        Assert.Equal("NewSensor", captured.Name);
        Assert.Equal("0x1234", captured.Address);
    }

    [Fact]
    public async Task GetUnsubscribedAddressesAsync_ReturnsCorrectList()
    {
        // Arrange
        var devices = new List<Device>
        {
            new() { Address = "0x1234", Subscribed = false },
            new() { Address = "0x5678", Subscribed = true }
        }.AsQueryable();

        var mockSet = devices.BuildMockDbSet();
        _repoMock.Setup(r => r.Query<Device>()).Returns(mockSet.Object);

        // Act
        var result = await _sut.GetUnsubscribedAddressesAsync();

        // Assert
        Assert.Single(result);
        Assert.Contains("0x1234", result);
    }

    [Fact]
    public async Task GetSubscribedAddressesAsync_ReturnsCorrectList()
    {
        // Arrange
        var devices = new List<Device>
        {
            new() { Address = "0x1234", Subscribed = false },
            new() { Address = "0x5678", Subscribed = true }
        }.AsQueryable();

        var mockSet = devices.BuildMockDbSet();
        _repoMock.Setup(r => r.Query<Device>()).Returns(mockSet.Object);

        // Act
        var result = await _sut.GetSubscribedAddressesAsync();

        // Assert
        Assert.Single(result);
        Assert.Contains("0x5678", result);
    }
}