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

public class DeviceFilterServiceTests
{
    private readonly Mock<IZigbeeRepository> _repoMock;
    private readonly Mock<IDeviceService> _deviceServiceMock;
    private readonly DeviceFilterService _sut;

    public DeviceFilterServiceTests()
    {
        _repoMock = new Mock<IZigbeeRepository>();
        _deviceServiceMock = new Mock<IDeviceService>();
        _sut = new DeviceFilterService(_repoMock.Object, _deviceServiceMock.Object);
    }

    [Fact]
    public async Task QueryDataFilterAsync_ReturnsFiltersForDevice()
    {
        _deviceServiceMock
            .Setup(d => d.AddressToIdAsync("0x1234"))
            .ReturnsAsync(42);

        var filters = new List<DeviceFilter>
        {
            new()
            {
                DeviceId = 42,
                FilterValue = "temperature",
                Device = new Device { Id = 42, Address = "0x1234" }
            },
            new()
            {
                DeviceId = 42,
                FilterValue = "humidity",
                Device = new Device { Id = 42, Address = "0x1234" }
            }
        }.AsQueryable();

        var mockSet = filters.BuildMockDbSet();

        _repoMock.Setup(r => r.Query<DeviceFilter>())
            .Returns(mockSet.Object);


        var result = await _sut.QueryDataFilterAsync("0x1234");


        Assert.NotNull(result);
        Assert.Equal(2, result.Count);
    }


    [Fact]
    public async Task NewFilterEntryAsync_DeviceNotFound_ThrowsException()
    {
        _deviceServiceMock.Setup(d => d.AddressToIdAsync("nonexistent"))
            .ReturnsAsync(null as int?);


        await Assert.ThrowsAsync<Exception>(() =>
            _sut.NewFilterEntryAsync("nonexistent", "temperature"));
    }

    [Fact]
    public async Task NewFilterEntryAsync_ValidData_CreatesFilter()
    {
        _deviceServiceMock.Setup(d => d.AddressToIdAsync("0x1234"))
            .ReturnsAsync(42);

        DeviceFilter captured = null!;
        _repoMock.Setup(r => r.CreateAsync(It.IsAny<DeviceFilter>(), It.IsAny<bool>(), true, default))
            .Callback<DeviceFilter, bool, bool, System.Threading.CancellationToken>((df, _, _, _) => captured = df)
            .Returns(Task.CompletedTask);

        _repoMock.Setup(r => r.SaveChangesAsync(true, default)).Returns(Task.FromResult(1));


        await _sut.NewFilterEntryAsync("0x1234", "temperature");


        Assert.NotNull(captured);
        Assert.Equal(42, captured.DeviceId);
        Assert.Equal("temperature", captured.FilterValue);
        _repoMock.Verify(r => r.SaveChangesAsync(true, default), Times.Once);
    }
}