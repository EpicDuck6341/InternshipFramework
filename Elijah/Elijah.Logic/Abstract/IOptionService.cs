using Elijah.Domain.Entities;

namespace Elijah.Logic.Abstract;

public interface IOptionService
{
    Task SetOptionsAsync(string address, string description, string currentValue, string property);
    Task AdjustOptionValueAsync(string address, string property, string currentValue);
    Task<List<ChangedOption>> GetChangedOptionValuesAsync(List<string> subscribedAddresses);
}