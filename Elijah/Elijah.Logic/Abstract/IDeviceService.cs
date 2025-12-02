using Elijah.Domain.Entities;

namespace Elijah.Logic.Abstract;

public interface IDeviceService
{
    Task<Device?> GetDeviceByAdressAsync(string address, bool allowNull = false);
    Task<string?> QueryDeviceNameAsync(string modelId);
    Task<string?> QueryDeviceAddressAsync(string name);
    Task<string?> QueryModelIdAsync(string address);
    // Task SetActiveStatusAsync(bool active, string address);
    Task SetSubscribedStatusAsync(bool subscribed, string address);
    Task SetActiveStatusAsync(bool active, string address);
    Task<bool> DevicePresentAsync(string modelId, string address);
    Task UnsubOnExitAsync();
    Task<List<string>> GetUnsubscribedAddressesAsync();
    Task<List<string>> GetSubscribedAddressesAsync();
    Task NewDeviceEntryAsync(string modelId, string newName, string address);
    
    Task<int?> AddressToIdAsync(string address);
    Task<List<string>> GetActiveAddressesAsync();
}
