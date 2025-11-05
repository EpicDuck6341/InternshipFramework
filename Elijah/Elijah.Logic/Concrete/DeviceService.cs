using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class DeviceService : IDeviceService
{
    
    private readonly ApplicationDbContext _db;

    public DeviceService(IExampleRepository repo)
    {
        _db = repo.DbContext;  
    }
    
    public async Task<string?> QueryDeviceNameAsync(string modelId)
    {
        return await _db.Devices
            .Where(d => d.ModelId == modelId)
            .Select(d => d.Name)
            .FirstOrDefaultAsync();
    }
    
    public async Task<string?> QueryDeviceAddressAsync(string name)
    {
        return await _db.Devices
            .Where(d => d.Name == name)
            .Select(d => d.Address)
            .FirstOrDefaultAsync();
    }
    
    public async Task<string?> QueryModelIDAsync(string address)
    {
        return await _db.Devices
            .Where(d => d.Address == address)
            .Select(d => d.ModelId)
            .FirstOrDefaultAsync();
    }
    
    public async Task SetActiveStatusAsync(bool active, string address)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Address == address);
        if (device != null)
        {
            device.Active = active;
            await _db.SaveChangesAsync();
        }
    }
    
    public async Task SetSubscribedStatusAsync(bool subscribed, string address)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Address == address);
        if (device != null)
        {
            device.Subscribed = subscribed;
            await _db.SaveChangesAsync();
        }
    }
    
    public async Task<bool> DevicePresentAsync(string modelID, string address)
    {
        bool exists = await _db.Devices.AnyAsync(d => d.Address == address);
        Console.WriteLine(exists ? "Device already present" : "Device not yet present");
        if (!exists) await ModelPresentAsync(modelID, address);
        return exists;
    }
    
    public async Task<bool> ModelPresentAsync(string modelID, string address)
    {
        bool exists = await _db.Devices.AnyAsync(d => d.ModelId == modelID);
        Console.WriteLine(exists ? "Model already present" : "Model not yet present");
        return exists;
    }
    
    public async Task UnsubOnExitAsync()
    {
        await _db.Devices.ForEachAsync(d => d.Subscribed = false);
        await _db.SaveChangesAsync();
        Console.WriteLine("All devices unsubscribed.");
    }
    public async Task<List<string>> GetUnsubscribedAddressesAsync()
    {
        return await _db.Devices
            .Where(d => !d.Subscribed)
            .Select(d => d.Address)
            .ToListAsync();
    }
    
    public async Task<List<string>> GetSubscribedAddressesAsync()
    {
        return await _db.Devices
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
        _db.Devices.Add(newDevice);
        await _db.SaveChangesAsync();
    }
}