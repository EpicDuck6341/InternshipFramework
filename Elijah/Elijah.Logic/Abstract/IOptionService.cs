using Elijah.Domain.Models;

namespace Elijah.Logic.Abstract;

// --------------------------------------- //
// Interface for device option management  //
// --------------------------------------- //
public interface IOptionService
{
    // ---------------------------------------- //
    // Creates a new option entry for a device  //
    // ---------------------------------------- //
    Task SetOptionsAsync(string address, string description, string currentValue, string property);

    // -------------------------------- //
    // Updates an existing option value //
    // -------------------------------- //
    Task AdjustOptionValueAsync(string address, string property, string currentValue);

    // ----------------------------------------- //
    // Retrieves and resets all changed options  //
    // ----------------------------------------- //
    Task<List<ChangedOption>> GetChangedOptionValuesAsync(List<string> subscribedAddresses);
}