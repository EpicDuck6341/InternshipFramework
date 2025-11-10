using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class OptionService(IZigbeeRepository repo) : IOptionService
{
    public async Task SetOptionsAsync(string address, string description, string currentValue, string property)
    {
        repo.CreateAsync(new Option
        {
            Address = address,
            Description = description,
            CurrentValue = currentValue,
            Property = property
        });

        await repo.SaveChangesAsync();
    }

    public async Task AdjustOptionValueAsync(string address, string property, string currentValue)
    {
        var option = await repo.Query<Option>()
            .FirstOrDefaultAsync(o => o.Address == address && o.Property == property);

        if (option != null)
        {
            option.CurrentValue = currentValue;
            option.Changed = true;
            await repo.SaveChangesAsync();
        }
    }

    public async Task<List<ChangedOption>> GetChangedOptionValuesAsync(List<string> subscribedAddresses)
    {
        if (subscribedAddresses == null || subscribedAddresses.Count == 0)
            return null;

        var changed = await repo.Query<Option>()
            .Where(o => o.Changed && subscribedAddresses.Contains(o.Address))
            .ToListAsync();

        List<ChangedOption> result = changed
            .Select(o => new ChangedOption(o.Address, o.Property, o.CurrentValue))
            .ToList();

        foreach (var o in changed)
            o.Changed = false;

        await repo.SaveChangesAsync();
        return result;
    }
}