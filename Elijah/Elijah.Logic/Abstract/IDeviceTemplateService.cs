using Elijah.Domain.Entities;

namespace Elijah.Logic.Concrete;

public interface IDeviceTemplateService
{
    Task CopyModelTemplateAsync(string modelId, string address);
    Task<DeviceTemplate> NewDvTemplateEntryAsync(string modelId, string name);
    public Task<bool> ModelPresentAsync(string modelId,string address);
}