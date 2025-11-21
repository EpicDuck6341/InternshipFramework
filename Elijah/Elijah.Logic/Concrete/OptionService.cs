using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class OptionService(IZigbeeRepository repo) : IOptionService
{
    public async Task SetOptionsAsync(string address, string description, string currentValue, string property)//bump
    {
        var device = await repo.Query<Device>().FirstOrDefaultAsync(device => device.Address == address);
        if(device == null)
            throw new Exception($"Device not found for address: {address}");
        await repo.CreateAsync(new Option
        {
            DeviceId = device.Id,
            Description = description,
            CurrentValue = currentValue,
            Property = property
        },saveChanges: true);
    }

    public async Task AdjustOptionValueAsync(string address, string property, string currentValue)
    {
        var option = await repo.Query<Option>()
            .FirstOrDefaultAsync(o => o.Device.Address == address && o.Property == property);//bump

        if (option != null)
        {
            option.CurrentValue = currentValue;
            option.IsProcessed = true;
            await repo.SaveChangesAsync();
        }
    }

    public async Task<List<ChangedOption>> GetChangedOptionValuesAsync(List<string> subscribedAddresses)
    {
        if (subscribedAddresses.Count == 0)
            return [];

        var changedOptions = await repo.Query<Option>().Include(option => option.Device)//bump
            .Where(o => o.IsProcessed &&  subscribedAddresses.Contains(o.Device.Address))
            .ToListAsync();
        
        changedOptions.ForEach(option => option.IsProcessed = false);
        await repo.SaveChangesAsync();
        
        return changedOptions.Select(option => new ChangedOption
        {
             Address = option.Device.Address,
             Property = option.Property,
             CurrentValue = option.CurrentValue
        }).ToList();
    }
}