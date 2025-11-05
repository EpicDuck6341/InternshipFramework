using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class DeviceFilterService : IDeviceFilterService
{
    
    private readonly ApplicationDbContext _db;

    public DeviceFilterService(IExampleRepository repo)
    {
        _db = repo.DbContext;  
    }

    
    public async Task<List<string>?> QueryDataFilterAsync(string address)
    {
        var filters = await _db.DeviceFilters
            .Where(f => f.Address == address)
            .Select(f => f.FilterValue)
            .ToListAsync();

        return filters.Any() ? filters : null;
    }
    
    public async Task NewFilterEntryAsync(string address, string filterValue, bool active)
    {
        _db.DeviceFilters.Add(new DeviceFilter()
        {
            Address = address,
            FilterValue = filterValue,
            Active = active
        });
        await _db.SaveChangesAsync();
    }
}