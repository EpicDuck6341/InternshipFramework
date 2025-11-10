using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class DeviceService(IZigbeeRepository repo) : IDeviceService
{
    
    
    public async Task<string?> QueryDeviceNameAsync(string modelId)
    {
         return (await repo.Query<Device>().FirstOrDefaultAsync(d => d.ModelId == modelId))?.Name; //CHECK

    }
    
    public async Task<string?> QueryDeviceAddressAsync(string name)//CHECK
    {
       return  (await repo.Query<Device>().FirstOrDefaultAsync(d => d.Name == name))?.Address;
    }
    
    public async Task<string?> QueryModelIDAsync(string address)
    {
        return (await repo.Query<Device>().FirstOrDefaultAsync(d => d.Address == address))?.ModelId;
    }
    
    public async Task SetActiveStatusAsync(bool active, string address)
    {
        var device = await repo.Query<Device>().FirstOrDefaultAsync(d => d.Address == address);
        if (device != null)
        {
            device.Active = active;
            await repo.SaveChangesAsync();
        }
    }

    public async Task SetSubscribedStatusAsync(bool subscribed, string address)
    {
        var device = await repo.Query<Device>().FirstOrDefaultAsync(d => d.Address == address);
        if (device != null)
        {
            device.Subscribed = subscribed;
            await repo.SaveChangesAsync();
        }
    }

    
    public async Task<bool> DevicePresentAsync(string modelID, string address)
    {
        bool exists = await repo.Query<Device>().AnyAsync(d => d.Address == address);
        Console.WriteLine(exists ? "Device already present" : "Device not yet present");
        if (!exists) await ModelPresentAsync(modelID, address);
        return exists;
    }

    public async Task<bool> ModelPresentAsync(string modelID, string address)
    {
        bool exists = await repo.Query<Device>().AnyAsync(d => d.ModelId == modelID);
        Console.WriteLine(exists ? "Model already present" : "Model not yet present");
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

    public async Task NewDeviceEntryAsync(string modelID, string newName, string address)
    {
        var newDevice = new Device
        {
            ModelId = modelID,
            Name = newName,
            Address = address
        };
        
        repo.CreateAsync(newDevice); //Save changes CHECK
        await repo.SaveChangesAsync();
    }

}