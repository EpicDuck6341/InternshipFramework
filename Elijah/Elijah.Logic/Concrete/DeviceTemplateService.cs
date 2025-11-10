using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class DeviceTemplateService(IZigbeeRepository repo) : IDeviceTemplateService
{
    public async Task CopyModelTemplateAsync(string modelID, string address)
    {
        // Get the device template by model ID
        var template = await repo.Query<DeviceTemplate>()
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

        repo.CreateAsync(newDevice);

        // Increment the number of active devices in the template
        template.NumberOfActive++;
        await repo.SaveChangesAsync();

        // Copy report templates into configured reports
        var templateReports = await repo.Query<ReportTemplate>()
                                        .Where(r => r.ModelId == modelID)
                                        .ToListAsync();

        foreach (var r in templateReports)
        {
            repo.CreateAsync(new ConfiguredReporting
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

        await repo.SaveChangesAsync();
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
}
