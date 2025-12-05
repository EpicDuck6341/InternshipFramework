namespace Elijah.Logic.Abstract;

// ----------------------------------------------- //
// Interface for device data filtering operations  //
// ----------------------------------------------- //
public interface IDeviceFilterService
{
    // --------------------------------------------- //
    // Retrieves filter values for a specific device //
    // --------------------------------------------- //
    Task<List<string>?> QueryDataFilterAsync(string address);

    // ---------------------------------------- //
    // Creates a new filter entry for a device  //
    // ---------------------------------------- //
    Task NewFilterEntryAsync(string address, string filterValue);
}