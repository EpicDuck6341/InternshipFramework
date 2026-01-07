using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FacilicomLogManager.Extensions;

namespace Elijah.Logic.Concrete;


// ---------------------------------------------------------------- //
// Core service for device management operations                    //
// Handles device lifecycle, subscriptions, and address resolution  //
// ---------------------------------------------------------------- //
public class DeviceService(
    ILogger<DeviceService> logger,
    IZigbeeRepository repo,
    IDeviceTemplateService deviceTemplate
) : IDeviceService
{
    // ---------------------------------------------------------------------------------------- //
    // Returns a Device object based on address, can be used for all properties of said device  //
    // ---------------------------------------------------------------------------------------- //
    public async Task<Device?> GetDeviceByAddressAsync(string address, bool allowNull = false)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Ophalen device op adres {address}")
            .SendLogInformation("GetDeviceByAddressAsync called - Address: {Address}, AllowNull: {AllowNull}", address, allowNull);

        var device = await repo.Query<Device>()
            .FirstOrDefaultAsync(d => d.Address == address);

        if (device == null && !allowNull)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Device {address} niet gevonden")
                .SendLogError("Device niet gevonden - Address: {Address}", address);
            throw new KeyNotFoundException($"Device with address '{address}' not found.");
        }

        logger
            .WithFacilicomContext(friendlyMessage: $"Device opgehaald: {device?.Name}")
            .SendLogInformation("GetDeviceByAddressAsync voltooid - DeviceId: {DeviceId}", device?.Id);

        return device;
    }

    // ------------------------------------------------- //
    // Returns the address of a device based on its name //
    // ------------------------------------------------- //
    public async Task<string?> QueryDeviceAddressAsync(string name)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Zoeken device adres voor {name}")
            .SendLogInformation("QueryDeviceAddressAsync called - Name: {Name}", name);

        var address = (await repo.Query<Device>()
            .FirstOrDefaultAsync(d => d.Name == name))?.Address;

        logger
            .WithFacilicomContext(friendlyMessage: $"Device adres gevonden: {address}")
            .SendLogInformation("QueryDeviceAddressAsync voltooid - Address: {Address}", address);

        return address;
    }

    // -------------------------------------------------------- //
    // Checks if a device exists, adds a template entry if not  //
    // -------------------------------------------------------- //
    public async Task<string> DevicePresentAsync(string modelId, string address)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Controleren device aanwezigheid {address}")
            .SendLogInformation("DevicePresentAsync called - ModelId: {ModelId}, Address: {Address}", modelId, address);

        var exists = await repo.Query<Device>()
            .AnyAsync(d => d.Address == address);
        string type;
        if (!exists)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Device {address} bestaat niet, template wordt aangemaakt")
                .SendLogInformation("Device bestaat niet, template wordt aangemaakt");
            type = await deviceTemplate.ModelPresentAsync(modelId, address);
        }
        else
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Device {address} bestaat al")
                .SendLogInformation("Device bestaat al");
            type = "deviceExist";
        }

        logger
            .WithFacilicomContext(friendlyMessage: $"Resultaat: {type}")
            .SendLogInformation("DevicePresentAsync voltooid - Type: {Type}", type);

        return type;
    }

    // ------------------------ //
    // Unsubscribe all devices  //
    // ------------------------ //
    public async Task UnsubOnExitAsync()
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Alle devices unsubscriben")
            .SendLogInformation("UnsubOnExitAsync started");

        await repo.Query<Device>()
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.Subscribed, false));
        await repo.SaveChangesAsync();
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Alle devices ge-unsubscribed")
            .SendLogInformation("UnsubOnExitAsync voltooid");
    }

    public async Task SetSubscribedAsync(string address)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Device {address} markeren als subscribed")
            .SendLogInformation("SetSubscribedAsync - Address: {Address}", address);

        await repo.Query<Device>()
            .Where(d => d.Address == address)
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.Subscribed, true));
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Device {address} gemarkeerd als subscribed")
            .SendLogInformation("SetSubscribedAsync voltooid");
    }

    // ----------------------------------------- //
    // Returns addresses of unsubscribed devices //
    // ----------------------------------------- //
    public async Task<List<string>> GetUnsubscribedAddressesAsync()
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Ophalen unsubscribed devices")
            .SendLogInformation("GetUnsubscribedAddressesAsync started");

        var addresses = await repo.Query<Device>()
            .Where(d => !d.Subscribed)
            .Select(d => d.Address)
            .ToListAsync();

        logger
            .WithFacilicomContext(friendlyMessage: $"Unsubscribed devices opgehaald: {addresses.Count}")
            .SendLogInformation("GetUnsubscribedAddressesAsync voltooid - Count: {Count}", addresses.Count);

        return addresses;
    }

    // --------------------------------------- //
    // Returns addresses of subscribed devices //
    // --------------------------------------- //
    public async Task<List<string>> GetSubscribedAddressesAsync()
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Ophalen subscribed devices")
            .SendLogInformation("GetSubscribedAddressesAsync started");

        var addresses = await repo.Query<Device>()
            .Where(d => d.Subscribed)
            .Select(d => d.Address)
            .ToListAsync();

        logger
            .WithFacilicomContext(friendlyMessage: $"Subscribed devices opgehaald: {addresses.Count}")
            .SendLogInformation("GetSubscribedAddressesAsync voltooid - Count: {Count}", addresses.Count);

        return addresses;
    }

    // ------------------------------------------------------ //
    // Returns addresses of all active (non-removed) devices  //
    // ------------------------------------------------------ //
    public async Task<List<string>> GetActiveAddressesAsync()
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Ophalen actieve devices")
            .SendLogInformation("GetActiveAddressesAsync started");

        var addresses = await repo.Query<Device>()
            .Where(d => !d.SysRemoved)
            .Select(d => d.Address)
            .ToListAsync();

        logger
            .WithFacilicomContext(friendlyMessage: $"Actieve devices opgehaald: {addresses.Count}")
            .SendLogInformation("GetActiveAddressesAsync voltooid - Count: {Count}", addresses.Count);

        return addresses;
    }

    // ------------------------------------------ //
    // Creates a new device entry with a template //
    // ------------------------------------------ //
    public async Task NewDeviceEntryAsync(string modelId, string newName, string address)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Nieuwe device entry aanmaken: {newName}")
            .SendLogInformation("Start NewDeviceEntryAsync - ModelId: {ModelId}, Name: {Name}, Address: {Address}", modelId, newName, address);

        var template = await deviceTemplate.NewDvTemplateEntryAsync(modelId, newName);
        if (template.Id == 0)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Template niet gevonden voor {modelId}")
                .SendLogError("DeviceTemplate niet gevonden - ModelId: {ModelId}", modelId);
            throw new KeyNotFoundException($"DeviceTemplate with ModelId '{modelId}' not found.");
        }

        var newDevice = new Device
        {
            DeviceTemplateId = template.Id,
            Name = newName,
            Address = address,
        };

        await repo.CreateAsync(newDevice);
        await repo.SaveChangesAsync();
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Device entry aangemaakt: {newName}")
            .SendLogInformation("NewDeviceEntryAsync voltooid - DeviceId: {DeviceId}", newDevice.Id);
    }
}