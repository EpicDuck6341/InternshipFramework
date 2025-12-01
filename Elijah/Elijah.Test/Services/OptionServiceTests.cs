using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Concrete;
using MockQueryable.Moq;
using Moq;

namespace Elijah.Test.Services;

public class OptionServiceTests
{
    private readonly Mock<IZigbeeRepository> _repoMock;
    private readonly OptionService _sut;

    public OptionServiceTests()
    {
        _repoMock = new Mock<IZigbeeRepository>();
        _sut = new OptionService(_repoMock.Object);
    }

    [Fact]
    public async Task SetOptionsAsync_DeviceNotFound_ThrowsException()
    {
        var devices = Enumerable.Empty<Device>().AsQueryable();
        _repoMock.Setup(r => r.Query<Device>()).Returns(devices.BuildMockDbSet().Object);

        await Assert.ThrowsAsync<Exception>(
            () => _sut.SetOptionsAsync("nonexistent", "desc", "value", "prop")
        );
    }

    [Fact]
    public async Task SetOptionsAsync_ValidData_CreatesOption()
    {
        var device = new Device { Id = 42, Address = "0x1234" };
        var devices = new List<Device> { device }.AsQueryable();
        _repoMock.Setup(r => r.Query<Device>()).Returns(devices.BuildMockDbSet().Object);

        Option captured = null!;
        _repoMock
            .Setup(r => r.CreateAsync(It.IsAny<Option>(), It.IsAny<bool>(), true, default))
            .Callback<Option, bool, bool, System.Threading.CancellationToken>(
                (o, _, _, _) => captured = o
            )
            .Returns(Task.CompletedTask);

        await _sut.SetOptionsAsync("0x1234", "Temperature", "22.5", "temp");

        Assert.NotNull(captured);
        Assert.Equal(42, captured.DeviceId);
        Assert.Equal("Temperature", captured.Description);
        Assert.Equal("22.5", captured.CurrentValue);
        Assert.Equal("temp", captured.Property);
    }

    [Fact]
    public async Task AdjustOptionValueAsync_OptionFound_UpdatesAndMarksProcessed()
    {
        var device = new Device { Id = 42, Address = "0x1234" };
        var option = new Option
        {
            DeviceId = 42,
            Property = "temp",
            CurrentValue = "20.0",
            IsProcessed = false,
            Device = device,
        };

        var options = new List<Option> { option }.AsQueryable();
        _repoMock.Setup(r => r.Query<Option>()).Returns(options.BuildMockDbSet().Object);
        _repoMock.Setup(r => r.SaveChangesAsync(true, default)).Returns(Task.FromResult(1));

        await _sut.AdjustOptionValueAsync("0x1234", "temp", "25.0");

        Assert.Equal("25.0", option.CurrentValue);
        Assert.True(option.IsProcessed);
        _repoMock.Verify(r => r.SaveChangesAsync(true, default), Times.Once);
    }

    [Fact]
    public async Task GetChangedOptionValuesAsync_NoSubscribedAddresses_ReturnsEmpty()
    {
        var result = await _sut.GetChangedOptionValuesAsync(new List<string>());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetChangedOptionValuesAsync_ChangedOptionsExist_ReturnsAndResets()
    {
        var subscribed = new List<string> { "0x1234" };
        var device = new Device { Id = 42, Address = "0x1234" };

        var options = new List<Option>
        {
            new()
            {
                DeviceId = 42,
                Device = device,
                Property = "temp",
                CurrentValue = "25.0",
                IsProcessed = true,
            },
        }.AsQueryable();

        _repoMock.Setup(r => r.Query<Option>()).Returns(options.BuildMockDbSet().Object);
        _repoMock.Setup(r => r.SaveChangesAsync(true, default)).Returns(Task.FromResult(1));

        var result = await _sut.GetChangedOptionValuesAsync(subscribed);

        Assert.Single(result);
        Assert.Equal("0x1234", result[0].Address);
        Assert.Equal("temp", result[0].Property);
        Assert.Equal("25.0", result[0].CurrentValue);
        Assert.False(options.First().IsProcessed);
    }
}
