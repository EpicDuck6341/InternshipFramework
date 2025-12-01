namespace Elijah.Logic.Abstract;

public interface IDeviceTemplateService
{
    Task CopyModelTemplateAsync(string modelId, string address);
    Task NewDVTemplateEntryAsync(string modelId, string name);
    public Task<bool> ModelPresentAsync(string modelId);
}
