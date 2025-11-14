using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class DeviceService(IZigbeeRepository repo, IDeviceTemplateService _deviceTemplate) : IDeviceService
{
    public async Task<int> AddressToIdAsync(string address)
    {
        return (await repo.Query<Device>().FirstOrDefaultAsync(d => d.Address == address)).Id; //CHECK
    }


    public async Task<string?> QueryDeviceNameAsync(string address)
    {
         return (await repo.Query<Device>().FirstOrDefaultAsync(d => d.Address == address))?.Name; //CHECK

    }
    
    public async Task<string?> QueryDeviceAddressAsync(string name)//CHECK
    {
       return  (await repo.Query<Device>().FirstOrDefaultAsync(d => d.Name == name))?.Address;
    }
    
    public async Task<string?> QueryModelIDAsync(string address)
    {
      
        return (await repo.Query<Device>()
            .Include(d => d.DeviceTemplate)  
            .FirstOrDefaultAsync(d => d.Address == address))?.DeviceTemplate?.ModelId;
    }
    
    
    //Now make use of the removed modifier REMINDER
    
    
    // public async Task SetActiveStatusAsync(bool active, string address)
    // {
    //     var device = await repo.Query<Device>().FirstOrDefaultAsync(d => d.Address == address);
    //     if (device != null)
    //     {
    //         device.Active = active;
    //         await repo.SaveChangesAsync();
    //     }
    // }

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
        if (!exists) await _deviceTemplate.ModelPresentAsync(modelID);
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
     
        var templateId = await repo.Query<DeviceTemplate>()
            .Where(t => t.ModelId == modelID)
            .Select(t => t.Id)
            .FirstOrDefaultAsync();
    
        if (templateId == 0)
            throw new Exception($"DeviceTemplate with ModelId '{modelID}' not found.");

        var newDevice = new Device
        {
            TemplateId = templateId, 
            Name = newName,
            Address = address
        };
    
        await repo.CreateAsync(newDevice);  
        await repo.SaveChangesAsync();
    }

}