using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class OptionService(IZigbeeRepository repo, IDeviceService deviceService) : IOptionService
{
    // ---------------------------------------- //
    // Creates a new Option entry for a device //
    // ---------------------------------------- //
    public async Task SetOptionsAsync(
        string address,
        string description,
        string currentValue,
        string property
    )
    {
        var device = await deviceService.GetDeviceByAdressAsync(address);

        await repo.CreateAsync(
            new Option
            {
                DeviceId = device!.Id,
                Description = description,
                CurrentValue = currentValue,
                Property = property,
            },
            saveChanges: true
        );
    }

    // --------------------------------------------------------------------- //
    // Updates the value of a single option for a device + marks processed   //
    // --------------------------------------------------------------------- //
    public async Task AdjustOptionValueAsync(string address, string property, string currentValue)
    {
        var option = await repo.Query<Option>()
            .FirstOrDefaultAsync(o => o.Device.Address == address && o.Property == property);

        if (option is null) 
            return;

        option.CurrentValue = currentValue;
        option.IsProcessed = true;
        await repo.SaveChangesAsync();
    }

    // -------------------------------------------------------------------------------- //
    // Returns all changed options for subscribed devices and resets IsProcessed flag   //
    // -------------------------------------------------------------------------------- //
    public async Task<List<ChangedOption>> GetChangedOptionValuesAsync(
        List<string> subscribedAddresses
    )
    {
        if (subscribedAddresses.Count == 0)
            return [];

        var changedOptions = await repo.Query<Option>()
            .Include(o => o.Device)
            .Where(o => o.IsProcessed && subscribedAddresses.Contains(o.Device.Address))
            .ToListAsync();

        changedOptions.ForEach(o => o.IsProcessed = false);
        await repo.SaveChangesAsync();

        return changedOptions
            .Select(o => new ChangedOption
            {
                Address = o.Device.Address,
                Property = o.Property,
                CurrentValue = o.CurrentValue
            })
            .ToList();
    }
}
