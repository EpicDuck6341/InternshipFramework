using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class OptionService : IOptionService
{
    
    private readonly ApplicationDbContext _db;

    public OptionService(IExampleRepository repo)
    {
        _db = repo.DbContext;  
    }
    
    public async Task SetOptionsAsync(string address, string description, string currentValue, string property)
        {
            _db.Options.Add(new Option()
            {
                Address = address,
                Description = description,
                CurrentValue = currentValue,
                Property = property
            });
            await _db.SaveChangesAsync();
        }
    
        public async Task AdjustOptionValueAsync(string address, string property, string currentValue)
        {
            var option = await _db.Options.FirstOrDefaultAsync(o =>
                o.Address == address && o.Property == property);

            if (option != null)
            {
                option.CurrentValue = currentValue;
                option.Changed = true;
                await _db.SaveChangesAsync();
            }
        
        }
    
        public async Task<List<ChangedOption>> GetChangedOptionValuesAsync(List<string> subscribedAddresses)
        {
            if (subscribedAddresses == null || subscribedAddresses.Count == 0)
                return null;

            var changed = await _db.Options
                .Where(o => o.Changed && subscribedAddresses.Contains(o.Address))
                .ToListAsync();

            List<ChangedOption> result = changed.Select(o => new ChangedOption(o.Address, o.Property, o.CurrentValue)).ToList();

            foreach (var o in changed)
                o.Changed = false;

            await _db.SaveChangesAsync();
            return result;
        }


}