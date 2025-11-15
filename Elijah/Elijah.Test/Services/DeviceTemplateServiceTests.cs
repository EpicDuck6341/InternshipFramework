using System.Linq;
using System.Threading.Tasks;
using MockQueryable.Moq;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Concrete;
using Moq;
using Xunit;

namespace Elijah.Logic.Tests.Services;

public class DeviceTemplateServiceTests
{
    private readonly Mock<IZigbeeRepository> _repoMock;
    private readonly DeviceTemplateService _sut;

    public DeviceTemplateServiceTests()
    {
        _repoMock = new Mock<IZigbeeRepository>();
        _sut = new DeviceTemplateService(_repoMock.Object);
    }

    [Fact]
    public async Task CopyModelTemplateAsync_TemplateNotFound_ThrowsException()
    {
        // Arrange
        var templates = Enumerable.Empty<DeviceTemplate>().AsQueryable();
        _repoMock.Setup(r => r.Query<DeviceTemplate>()).Returns(templates.BuildMockDbSet().Object);

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _sut.CopyModelTemplateAsync("model123", "0x1234"));
    }

    [Fact]
    public async Task CopyModelTemplateAsync_ValidData_CopiesTemplate()
    {
        // Arrange
        var template = new DeviceTemplate
        {
            Id = 10,
            ModelId = "model123",
            Name = "Sensor",
            NumberOfActive = 2
        };

        _repoMock.Setup(r => r.Query<DeviceTemplate>())
            .Returns(new List<DeviceTemplate> { template }.AsQueryable().BuildMockDbSet().Object);
        
        _repoMock.Setup(r => r.Query<ConfiguredReporting>())
            .Returns(new List<ConfiguredReporting>().AsQueryable().BuildMockDbSet().Object);

        Device capturedDevice = null!;

        _repoMock.Setup(r => r.CreateAsync(It.IsAny<Device>(), It.IsAny<bool>(), true, default))
            .Callback<Device, bool, bool, CancellationToken>((d, _, _, _) => capturedDevice = d)
            .Returns(Task.CompletedTask);

        _repoMock.Setup(r => r.SaveChangesAsync(true, default))
            .ReturnsAsync(1);

        // Act
        await _sut.CopyModelTemplateAsync("model123", "0x1234");

        // Assert
        Assert.NotNull(capturedDevice);
        Assert.Equal("Sensor3", capturedDevice.Name);
        Assert.Equal(3, template.NumberOfActive);
    }

    [Fact]
    public async Task EnsureTemplateExistsAsync_TemplateExists_ReturnsTrue()
    {
        // Arrange
        var templates = new List<DeviceTemplate> { new() { ModelId = "model123" } }.AsQueryable();
        _repoMock.Setup(r => r.Query<DeviceTemplate>()).Returns(templates.BuildMockDbSet().Object);

        // Act
        var result = await _sut.EnsureTemplateExistsAsync("model123");

        // Assert
        Assert.True(result);
        _repoMock.Verify(r => r.CreateAsync(It.IsAny<DeviceTemplate>(), It.IsAny<bool>(), true, default), Times.Never);
    }

    [Fact]
    public async Task EnsureTemplateExistsAsync_TemplateNotExists_CreatesPlaceholder()
    {
        // Arrange
        var templates = Enumerable.Empty<DeviceTemplate>().AsQueryable();
        _repoMock.Setup(r => r.Query<DeviceTemplate>()).Returns(templates.BuildMockDbSet().Object);

        DeviceTemplate captured = null!;
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<DeviceTemplate>(), It.IsAny<bool>(), true, default))
            .Callback<DeviceTemplate, bool, bool, System.Threading.CancellationToken>((dt, _, _, _) => captured = dt)
            .Returns(Task.CompletedTask);

        _repoMock.Setup(r => r.SaveChangesAsync(true, default)).Returns(Task.FromResult(1));

        // Act
        var result = await _sut.EnsureTemplateExistsAsync("model123");

        // Assert
        Assert.False(result);
        Assert.NotNull(captured);
        Assert.Equal("model123", captured.ModelId);
        Assert.Equal("Model model123", captured.Name);
        Assert.Equal(1, captured.NumberOfActive);
    }

    [Fact]
    public async Task ModelPresentAsync_ReturnsCorrectValue()
    {
        // Arrange
        var templates = new List<DeviceTemplate> { new() { ModelId = "model123" } }.AsQueryable();
        _repoMock.Setup(r => r.Query<DeviceTemplate>()).Returns(templates.BuildMockDbSet().Object);

        // Act
        var result = await _sut.ModelPresentAsync("model123");

        // Assert
        Assert.True(result);
    }
}