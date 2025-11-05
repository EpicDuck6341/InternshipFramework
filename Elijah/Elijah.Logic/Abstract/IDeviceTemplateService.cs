namespace Elijah.Logic.Concrete;

public interface IDeviceTemplateService
{
    Task CopyModelTemplateAsync(string modelID, string address);
    Task NewDVTemplateEntryAsync(string modelID, string name);
}