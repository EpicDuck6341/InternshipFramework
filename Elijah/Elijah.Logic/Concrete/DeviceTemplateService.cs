using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class DeviceTemplateService(IZigbeeRepository repo) : IDeviceTemplateService
{
    public async Task CopyModelTemplateAsync(string modelID, string address)
    {
        
        var template = await repo.Query<DeviceTemplate>()
                                 .FirstOrDefaultAsync(t => t.ModelId == modelID);

        if (template == null)
            throw new Exception($"DeviceTemplate with ModelId '{modelID}' not found.");

        
        string newName = $"{template.Name}{template.NumberOfActive + 1}";
        var newDevice = new Device
        {
            TemplateId = template.Id,  
            Name = newName,
            Address = address
        };
        
        await repo.CreateAsync(newDevice);

     
        template.NumberOfActive++;

      
        await repo.SaveChangesAsync();

       
        var templateReports = await repo.Query<ConfiguredReporting>()
                                        .Where(r => r.IsTemplate && r.Device.DeviceTemplate.ModelId == modelID)
                                        .ToListAsync();
        
        foreach (var templateReport in templateReports)
        {
            await repo.CreateAsync(new ConfiguredReporting
            {
                DeviceId = newDevice.Id,
                Cluster = templateReport.Cluster,
                Attribute = templateReport.Attribute,
                MaximumReportInterval = templateReport.MaximumReportInterval,
                MinimumReportInterval = templateReport.MinimumReportInterval,
                ReportableChange = templateReport.ReportableChange,
                Endpoint = templateReport.Endpoint,
                IsTemplate = false  
            });
        }
        
        await repo.SaveChangesAsync();
    }

    
    public async Task<bool> EnsureTemplateExistsAsync(string modelID)
    {
        bool exists = await repo.Query<DeviceTemplate>().AnyAsync(d => d.ModelId == modelID);
        if (!exists)
        {
            Console.WriteLine($"Model {modelID} not present. Creating placeholder template.");
            await NewDVTemplateEntryAsync(modelID, $"Model {modelID}");
        }
        return exists;
    }

    public async Task NewDVTemplateEntryAsync(string modelID, string name)
    {
        repo.CreateAsync(new DeviceTemplate
        {
            ModelId = modelID,
            Name = name,
            NumberOfActive = 1
        });
        await repo.SaveChangesAsync();
    }
    
    public async Task<bool> ModelPresentAsync(string modelID)
    {
        bool exists = await repo.Query<DeviceTemplate>().AnyAsync(d => d.ModelId == modelID);
        Console.WriteLine(exists ? "Model already present" : "Model not yet present");
        return exists;
    }
}
