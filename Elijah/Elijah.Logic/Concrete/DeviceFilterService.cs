using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;


// ---------------------------------------------------------------- //
// Service for managing device data filters                         //
// Controls which payload properties are processed for each device  //
// ---------------------------------------------------------------- //
public class DeviceFilterService(IZigbeeRepository repo, IDeviceService deviceService)
    : IDeviceFilterService
{
    // --------------------------------------------------------------------------------- //
    // Returns a list of all filter that can be applied to the sent payload of a device  //
    // --------------------------------------------------------------------------------- //
    public async Task<List<string>?> QueryDataFilterAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return new List<string>();

        return await repo.Query<DeviceFilter>()
            .Where(filter => filter.Device.Address == address)
            .Select(filter => filter.FilterValue)
            .ToListAsync();
    }

    // ------------------------------------------------------------ //
    // Creates new filter and relates it back to an address/device  //
    // ------------------------------------------------------------ //
    public async Task NewFilterEntryAsync(string address, string filterValue)
    {
        var device = await deviceService.GetDeviceByAddressAsync(address);
        if (device == null)
            return; 

        await repo.CreateAsync(
            new DeviceFilter
            {
                DeviceId = device.Id,
                FilterValue = filterValue
            });

        await repo.SaveChangesAsync();
    }
}
