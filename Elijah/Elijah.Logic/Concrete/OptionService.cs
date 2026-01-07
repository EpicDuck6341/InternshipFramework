using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Domain.Models;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FacilicomLogManager.Extensions;

namespace Elijah.Logic.Concrete;


// ------------------------------------------------ //
// Device option management service                 //
// Tracks and updates device configuration options  //
// ------------------------------------------------ //
public class OptionService(
    IZigbeeRepository repo, 
    IDeviceService deviceService,
    ILogger<OptionService> logger) : IOptionService
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
        logger
            .WithFacilicomContext(friendlyMessage: $"Option instellen voor device {address}")
            .SendLogInformation("SetOptionsAsync - Address: {Address}, Property: {Property}, Value: {Value}", address, property, currentValue);

        var device = await deviceService.GetDeviceByAddressAsync(address);

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
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Option opgeslagen")
            .SendLogInformation("SetOptionsAsync voltooid - DeviceId: {DeviceId}, Property: {Property}", device.Id, property);
    }

    // --------------------------------------------------------------------- //
    // Updates the value of a single option for a device + marks processed   //
    // --------------------------------------------------------------------- //
    public async Task AdjustOptionValueAsync(string address, string property, string currentValue)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Option aanpassen voor {address}")
            .SendLogInformation("AdjustOptionValueAsync - Address: {Address}, Property: {Property}, Value: {Value}", address, property, currentValue);

        var option = await repo.Query<Option>()
            .FirstOrDefaultAsync(o => o.Device.Address == address && o.Property == property);

        if (option is null) 
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Option niet gevonden")
                .SendLogWarning("Option niet gevonden bij AdjustOptionValueAsync - Address: {Address}, Property: {Property}", address, property);
            return;
        }

        option.CurrentValue = currentValue;
        option.IsProcessed = true;
        await repo.SaveChangesAsync();
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Option aangepast")
            .SendLogInformation("AdjustOptionValueAsync voltooid - OptionId: {OptionId}", option.Id);
    }

    // -------------------------------------------------------------------------------- //
    // Returns all changed options for subscribed devices and resets IsProcessed flag   //
    // -------------------------------------------------------------------------------- //
    public async Task<List<ChangedOption>> GetChangedOptionValuesAsync(
        List<string> subscribedAddresses
    )
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Ophalen gewijzigde options")
            .SendLogInformation("GetChangedOptionValuesAsync started - Aantal addresses: {Count}", subscribedAddresses.Count);

        if (subscribedAddresses.Count == 0)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Geen geabonneerde devices")
                .SendLogInformation("Geen subscribed addresses");
            return [];
        }

        var changedOptions = await repo.Query<Option>()
            .Include(o => o.Device)
            .Where(o => o.IsProcessed && subscribedAddresses.Contains(o.Device.Address))
            .ToListAsync();

        changedOptions.ForEach(o => o.IsProcessed = false);
        await repo.SaveChangesAsync();

        logger
            .WithFacilicomContext(friendlyMessage: $"Gewijzigde options opgehaald: {changedOptions.Count}")
            .SendLogInformation("GetChangedOptionValuesAsync voltooid - Aantal options: {Count}", changedOptions.Count);

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