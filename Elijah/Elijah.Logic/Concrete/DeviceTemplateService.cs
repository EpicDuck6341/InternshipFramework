using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class DeviceTemplateService(IZigbeeRepository repo) : IDeviceTemplateService
{
    // ------------------------------------------------------------ //
    // Copies a device template to a new device with the given address //
    // ------------------------------------------------------------ //
    public async Task CopyModelTemplateAsync(string modelId, string address)
    {
        var template = await repo.Query<DeviceTemplate>()
            .FirstOrDefaultAsync(t => t.ModelId == modelId);

        if (template == null)
            throw new KeyNotFoundException($"DeviceTemplate with ModelId '{modelId}' not found.");

        var newName = $"{template.Name}{template.NumberOfActive + 1}";
        var newDevice = new Device
        {
            DeviceTemplateId = template.Id,
            Name = newName,
            Address = address
        };

        template.NumberOfActive++;

        await repo.CreateAsync(newDevice, saveChanges: true);

        var templateReports = await repo.Query<ConfiguredReporting>()
            .Where(r => r.IsTemplate && r.Device.DeviceTemplate.ModelId == modelId)
            .ToListAsync();

        foreach (var templateReport in templateReports)
        {
            await repo.CreateAsync(
                new ConfiguredReporting
                {
                    DeviceId = newDevice.Id,
                    Cluster = templateReport.Cluster,
                    Attribute = templateReport.Attribute,
                    MaximumReportInterval = templateReport.MaximumReportInterval,
                    MinimumReportInterval = templateReport.MinimumReportInterval,
                    ReportableChange = templateReport.ReportableChange,
                    Endpoint = templateReport.Endpoint,
                    IsTemplate = false,
                }
            );
        }

        await repo.SaveChangesAsync();
    }

    // ------------------------------------------------------------ //
    // Creates a new device template entry                           //
    // ------------------------------------------------------------ //
    public async Task<DeviceTemplate> NewDvTemplateEntryAsync(string modelId, string name)
    {
        var template = new DeviceTemplate
        {
            ModelId = modelId,
            Name = name,
            NumberOfActive = 1,
        };

        await repo.CreateAsync(template);

        Console.WriteLine($"Template created: {name} ({modelId}) with ID {template.Id}");
        return template;
    }

    // ------------------------------------------------------------ //
    // Checks if a model template exists; copies template if present //
    // ------------------------------------------------------------ //
    public async Task<bool> ModelPresentAsync(string modelId, string address)
    {
        var templateExists = await repo.Query<DeviceTemplate>()
            .AnyAsync(d => d.ModelId == modelId);

        Console.WriteLine(templateExists ? "Model already present" : "Model not yet present");

        if (templateExists)
            await CopyModelTemplateAsync(modelId, address);

        return templateExists;
    }
}
