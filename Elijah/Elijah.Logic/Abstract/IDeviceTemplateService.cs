using Elijah.Domain.Entities;

namespace Elijah.Logic.Abstract;

// ---------------------------------------- //
// Interface for device template management //
// ---------------------------------------- //
public interface IDeviceTemplateService
{
    // ------------------------------------------------ //
    // Duplicates a template for a new device instance  //
    // ------------------------------------------------ //
    Task CopyModelTemplateAsync(string modelId, string address);

    // ------------------------------------ //
    // Creates a new device template entry  //
    // ------------------------------------ //
    Task<DeviceTemplate> NewDvTemplateEntryAsync(string modelId, string name);

    // -------------------------------------------------------- //
    // Validates template existence and triggers copy if found  //
    // -------------------------------------------------------- //
    Task<string> ModelPresentAsync(string modelId, string address);
}