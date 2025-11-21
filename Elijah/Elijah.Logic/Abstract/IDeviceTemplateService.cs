using Elijah.Domain.Entities;

namespace Elijah.Logic.Concrete;

public interface IDeviceTemplateService
{
    Task CopyModelTemplateAsync(string modelID, string address);
    Task<DeviceTemplate> NewDVTemplateEntryAsync(string modelID, string name);
    public Task<bool> ModelPresentAsync(string modelID,string address);
}