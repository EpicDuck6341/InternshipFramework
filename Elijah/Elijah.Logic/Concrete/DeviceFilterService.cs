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
    {
        return await repo.Query<DeviceFilter>()
            .Where(f => f.Device.Address == address && f.SysRemoved.Equals(false))
            .Select(f => f.FilterValue)
            .ToListAsync();
    }



    public async Task NewFilterEntryAsync(string address, string filterValue)
    {
       
        int? deviceId = await _device.AddressToIdAsync(address);
        
        if (deviceId == null)
            throw new Exception($"Device with address '{address}' not found.");

      
        await repo.CreateAsync(new DeviceFilter
        {
            DeviceId = (int)deviceId, 
            FilterValue = filterValue
            // IsActive = true  //Staat het filter aan ja of nee....
        });
       
        await repo.SaveChangesAsync();
    }
}
