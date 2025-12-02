using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using FacilicomLogManager.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Elijah.Logic.Concrete;

public class DeviceService(
    ILogger<DeviceService> logger,
    IZigbeeRepository repo,
    IDeviceTemplateService deviceTemplate
) : IDeviceService
{
    public async Task<Device?> GetDeviceByAdressAsync(string address, bool allowNull = false)
    {
        //Voorbeeld:
        logger
            .WithFacilicomContext(friendlyMessage: "Klein kort bericht")
            .SendLogWarning("uitgebreider bericht {Address}", address); //test

        var device = await repo.Query<Device>().FirstOrDefaultAsync(d => d.Address == address);

        if (device == null && !allowNull)
            throw new Exception($"Device with address '{address}' not found.");

        return device;
    }
    
    public async Task<Device> GetDeviceByNameAsync(string name) //CHECK
    {
        var device = await repo.Query<Device>().FirstOrDefaultAsync(d => d.Name == name);

        if (device == null)
            throw new Exception($"Device with name '{name}' not found.");

        return device;
    }
    
    public async Task<int?> AddressToIdAsync(string address)
    {
        return (await repo.Query<Device>().FirstOrDefaultAsync(d => d.Address == address)).Id; //CHECK
    }


    public async Task<string?> QueryDeviceNameAsync(string address)
    {
        return (await repo.Query<Device>().FirstOrDefaultAsync(d => d.Address == address))?.Name; //CHECK
    }

    public async Task<string?> QueryDeviceAddressAsync(string name) //CHECK
    {
        return (await repo.Query<Device>().FirstOrDefaultAsync(d => d.Name == name))?.Address;
    }

    public async Task<string?> QueryModelIdAsync(string address)
    {
        var device = await repo.Query<Device>()
            .Include(i => i.DeviceTemplate)
            .FirstOrDefaultAsync(d => d.Address == address);

        if (device == null)
            throw new Exception($"Device with address '{address}' not found.");

        return device.DeviceTemplate.ModelId;
    }



    //Now make use of the removed modifier REMINDER


    public async Task SetActiveStatusAsync(bool active, string address)
    {
        var device = await repo.Query<Device>().FirstOrDefaultAsync(d => d.Address == address);
        if (device != null)
        {
            device.SysRemoved = active;
            await repo.SaveChangesAsync();
        }
    }
    
    

    public async Task SetSubscribedStatusAsync(bool subscribed, string address)
    {
        var device = await GetDeviceByAdressAsync(address);

        device.Subscribed = subscribed;

        await repo.SaveChangesAsync();
    }

    public async Task<bool> DevicePresentAsync(string modelId, string address)
    {
        bool exists = false;
        try
        {
            exists = await repo.Query<Device>().AnyAsync(d => d.Address == address);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        Console.WriteLine(exists ? "Device already present" : "Device not yet present");
        if (!exists)
            await deviceTemplate.ModelPresentAsync(modelId,address);
        return exists;
    }

    public async Task UnsubOnExitAsync()
    {
        await repo.Query<Device>().ForEachAsync(d => d.Subscribed = false);
        await repo.SaveChangesAsync();
        Console.WriteLine("All devices unsubscribed.");
    }

    public async Task<List<string>> GetUnsubscribedAddressesAsync()
    {
        return await repo.Query<Device>()
            .Where(d => !d.Subscribed)
            .Select(d => d.Address)
            .ToListAsync();
    }

    public async Task<List<string>> GetSubscribedAddressesAsync()
    {
        return await repo.Query<Device>()
            .Where(d => d.Subscribed)
            .Select(d => d.Address)
            .ToListAsync();
    }
    
    public async Task<List<string>> GetActiveAddressesAsync()
    {
        return await repo.Query<Device>()
            .Where(d => d.SysRemoved.Equals(false))
            .Select(d => d.Address)
            .ToListAsync();
    }

    public async Task NewDeviceEntryAsync(string modelId, string newName, string address)
    {
        var template = await deviceTemplate.NewDvTemplateEntryAsync(modelId, newName);
        
        if (template.Id == 0)
            throw new Exception($"DeviceTemplate with ModelId '{modelId}' not found.");
        
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
