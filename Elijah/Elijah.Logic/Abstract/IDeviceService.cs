namespace Elijah.Logic.Abstract;

public interface IDeviceService
{
    Task<string?> QueryDeviceNameAsync(string modelId);
    Task<string?> QueryDeviceAddressAsync(string name);
    Task<string?> QueryModelIDAsync(string address);
    // Task SetActiveStatusAsync(bool active, string address);
    Task SetSubscribedStatusAsync(bool subscribed, string address);
    Task<bool> DevicePresentAsync(string modelID, string address);
    Task UnsubOnExitAsync();
    Task<List<string>> GetUnsubscribedAddressesAsync();
    Task<List<string>> GetSubscribedAddressesAsync();
    Task NewDeviceEntryAsync(string modelID, string newName, string address);
    
    Task<int> AddressToIdAsync(string address);
}
