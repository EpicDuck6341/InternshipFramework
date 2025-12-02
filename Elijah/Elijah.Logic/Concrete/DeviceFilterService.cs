using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class DeviceFilterService(IZigbeeRepository repo, IDeviceService deviceService)
    : IDeviceFilterService
{
    public async Task<List<string>?> QueryDataFilterAsync(string address) =>
        await repo.Query<DeviceFilter>()
            .Where(filter => filter.Device.Address == address)
            .Select(filter => filter.FilterValue)
            .ToListAsync();

    public async Task NewFilterEntryAsync(string address, string filterValue)
    {
        var device = await deviceService.GetDeviceByAdressAsync(address);
        try
        {
            await repo.CreateAsync(
                new DeviceFilter
                {
                    DeviceId = device!.Id,
                    FilterValue = filterValue
                }
            );
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

        await repo.SaveChangesAsync();
    }
}
