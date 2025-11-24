namespace Elijah.Logic.Abstract;

public interface IDeviceFilterService
{
    Task<List<string>?> QueryDataFilterAsync(string address);
    Task NewFilterEntryAsync(string address, string filterValue);
}