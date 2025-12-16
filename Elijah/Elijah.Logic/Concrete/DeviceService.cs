using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;


// ---------------------------------------------------------------- //
// Core service for device management operations                    //
// Handles device lifecycle, subscriptions, and address resolution  //
// ---------------------------------------------------------------- //
public class DeviceService(
    IZigbeeRepository repo,
    IDeviceTemplateService deviceTemplate
) : IDeviceService
{
    // ---------------------------------------------------------------------------------------- //
    // Returns a Device object based on address, can be used for all properties of said device  //
    // ---------------------------------------------------------------------------------------- //
    public async Task<Device?> GetDeviceByAddressAsync(string address, bool allowNull = false)
    {
        var device = await repo.Query<Device>()
            .FirstOrDefaultAsync(d => d.Address == address);

        if (device == null && !allowNull)
            throw new KeyNotFoundException($"Device with address '{address}' not found.");

        return device;
    }

    // ------------------------------------------------- //
    // Returns the address of a device based on its name //
    // ------------------------------------------------- //
    public async Task<string?> QueryDeviceAddressAsync(string name)
    {
        return (await repo.Query<Device>()
            .FirstOrDefaultAsync(d => d.Name == name))?.Address;
    }

    // -------------------------------------------------------- //
    // Checks if a device exists, adds a template entry if not  //
    // -------------------------------------------------------- //
    public async Task<string> DevicePresentAsync(string modelId, string address)
    {
        var exists = await repo.Query<Device>()
            .AnyAsync(d => d.Address == address);
        string type;
        if (!exists)
           type = await deviceTemplate.ModelPresentAsync(modelId, address);
        else
        {
            type = "deviceExist";
        }

        return type;
    }

    // ------------------------ //
    // Unsubscribe all devices  //
    // ------------------------ //
    public async Task UnsubOnExitAsync()
    {
        await repo.Query<Device>()
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.Subscribed, false));
        await repo.SaveChangesAsync();
    }

    public async Task SetSubscribedAsync(string address)
    {
        await repo.Query<Device>()
            .Where(d => d.Address == address)
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.Subscribed, true));
    }

    // ----------------------------------------- //
    // Returns addresses of unsubscribed devices //
    // ----------------------------------------- //
    public async Task<List<string>> GetUnsubscribedAddressesAsync()
    {
        return await repo.Query<Device>()
            .Where(d => !d.Subscribed)
            .Select(d => d.Address)
            .ToListAsync();
    }

    // --------------------------------------- //
    // Returns addresses of subscribed devices //
    // --------------------------------------- //
    public async Task<List<string>> GetSubscribedAddressesAsync()
    {
        return await repo.Query<Device>()
            .Where(d => d.Subscribed)
            .Select(d => d.Address)
            .ToListAsync();
    }

    // ------------------------------------------------------ //
    // Returns addresses of all active (non-removed) devices  //
    // ------------------------------------------------------ //
    public async Task<List<string>> GetActiveAddressesAsync()
    {
        return await repo.Query<Device>()
            .Where(d => !d.SysRemoved)
            .Select(d => d.Address)
            .ToListAsync();
    }

    // ------------------------------------------ //
    // Creates a new device entry with a template //
    // ------------------------------------------ //
    public async Task NewDeviceEntryAsync(string modelId, string newName, string address)
    {
        var template = await deviceTemplate.NewDvTemplateEntryAsync(modelId, newName);
        if (template.Id == 0)
            throw new KeyNotFoundException($"DeviceTemplate with ModelId '{modelId}' not found.");

        var newDevice = new Device
        {
            DeviceTemplateId = template.Id,
            Name = newName,
            Address = address,
        };

        await repo.CreateAsync(newDevice);
        await repo.SaveChangesAsync();
    }
}
