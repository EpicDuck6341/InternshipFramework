using System.Linq.Dynamic.Core;
using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class DeviceFilterService(IZigbeeRepository repo,IDeviceService _device) : IDeviceFilterService
{
    public async Task<List<string>?> QueryDataFilterAsync(string address)
        => await repo.Query<DeviceFilter>().Where(filter => filter.Device.Address == address)
            .Select(filter => filter.FilterValue).ToListAsync(); //bump


    public async Task NewFilterEntryAsync(string address, string filterValue, bool active)
    {
        
        var deviceId = await _device.AddressToIdAsync(address);
        if (deviceId == null)
            throw new Exception($"Device with address '{address}' not found.");

        
        await repo.CreateAsync(new DeviceFilter
        {
            DeviceId = deviceId, 
            FilterValue = filterValue,
            IsActive = active  //Staat het filter aan ja of nee....
        });

        await repo.SaveChangesAsync();
    }
}
