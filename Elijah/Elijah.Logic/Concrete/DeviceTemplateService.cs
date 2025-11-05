using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class DeviceTemplateService : IDeviceTemplateService
{
    private readonly ApplicationDbContext _db;

    public DeviceTemplateService(IExampleRepository repo)
    {
        _db = repo.DbContext;  
    }
    
    public async Task CopyModelTemplateAsync(string modelID, string address)
    {
        // Get the device template by model ID
        var template = await _db.DeviceTemplates
            .FirstOrDefaultAsync(t => t.ModelId == modelID);

        if (template == null)
            return;

        // Create the new device directly here
        string newName = $"{template.Name}{template.NumberOfActive}";
        var newDevice = new Device
        {
            ModelId = modelID,
            Name = newName,
            Address = address
        };

        _db.Devices.Add(newDevice);

        // Increment the number of active devices in the template
        template.NumberOfActive++;
        await _db.SaveChangesAsync();

        // Copy report templates into configured reports
        var templateReports = await _db.ReportTemplates
            .Where(r => r.ModelId == modelID)
            .ToListAsync();

        foreach (var r in templateReports)
        {
            _db.ConfiguredReportings.Add(new ConfiguredReporting
            {
                Address = address,
                Cluster = r.Cluster,
                Attribute = r.Attribute,
                MaximumReportInterval = r.MaximumReportInterval,
                MinimumReportInterval = r.MinimumReportInterval,
                ReportableChange = r.ReportableChange,
                Endpoint = r.Endpoint
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task NewDVTemplateEntryAsync(string modelID, string name)
    {
        _db.DeviceTemplates.Add(new DeviceTemplate
        {
            ModelId = modelID,
            Name = name,
            NumberOfActive = 1
        });
        await _db.SaveChangesAsync();
    }

}
