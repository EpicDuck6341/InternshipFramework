using Elijah.Domain.Entities;

namespace Elijah.Logic.Abstract;

// ------------------------------------------------------------ //
// Interface for core device management operations             //
// ------------------------------------------------------------ //
public interface IDeviceService
{
    // ---------------------------------- //
    // Retrieves a device by its address  //
    // ---------------------------------- //
    Task<Device?> GetDeviceByAddressAsync(string address, bool allowNull = false);

    // ----------------------------------- //
    // Finds a device address by its name  //
    // ----------------------------------- //
    Task<string?> QueryDeviceAddressAsync(string name);

    // ------------------------------------------------------- //
    // Verifies device existence, creates template if missing  //
    // ------------------------------------------------------- //
    Task<bool> DevicePresentAsync(string modelId, string address);

    // ------------------------------------------- //
    // Unsubscribes all devices on system shutdown //
    // ------------------------------------------- //
    Task UnsubOnExitAsync();

    // ------------------------------------------ //
    // Gets addresses of all unsubscribed devices //
    // ------------------------------------------ //
    Task<List<string>> GetUnsubscribedAddressesAsync();

    // ----------------------------------------- //
    // Gets addresses of all subscribed devices  //
    // ----------------------------------------- //
    Task<List<string>> GetSubscribedAddressesAsync();

    // ------------------------------------- //
    // Creates a new device from a template  //
    // ------------------------------------- //
    Task NewDeviceEntryAsync(string modelId, string newName, string address);

    // --------------------------------------------------- //
    // Gets addresses of all active (non-removed) devices  //
    // --------------------------------------------------- //
    Task<List<string>> GetActiveAddressesAsync();
}