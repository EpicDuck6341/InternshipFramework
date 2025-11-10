using System.Linq.Dynamic.Core;
using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class DeviceFilterService(IZigbeeRepository repo): IDeviceFilterService
{
 
    
    public async Task<List<string>?> QueryDataFilterAsync(string address)
    {
        var result = await repo.Query<DeviceFilter>().FirstOrDefault(f => f.Address == address)?.FilterValue.ToDynamicListAsync<String>()!;
        return result.Any() ? result : null;
    }
    
    public async Task NewFilterEntryAsync(string address, string filterValue, bool active)
    {
        repo.CreateAsync(new DeviceFilter
        {
            Address = address,
            FilterValue = filterValue,
            Active = active
        });

        await repo.SaveChangesAsync();
    }
}