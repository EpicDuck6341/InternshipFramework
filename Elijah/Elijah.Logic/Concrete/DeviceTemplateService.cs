using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FacilicomLogManager.Extensions;

namespace Elijah.Logic.Concrete;

// ------------------------------------------------------------ //
// Service for managing device templates and model replication  //
// ------------------------------------------------------------ //
public class DeviceTemplateService(
    IZigbeeRepository repo,
    ILogger<DeviceTemplateService> logger) : IDeviceTemplateService
{
    // ---------------------------------------------------------------- //
    // Copies a device template to a new device with the given address  //
    // ---------------------------------------------------------------- //
    public async Task CopyModelTemplateAsync(string modelId, string address)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Kopiëren template voor model {modelId}")
            .SendLogInformation("Start CopyModelTemplateAsync - ModelId: {ModelId}, Address: {Address}", modelId, address);

        var template = await repo.Query<DeviceTemplate>()
            .FirstOrDefaultAsync(t => t.ModelId == modelId);

        if (template == null)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Template niet gevonden voor {modelId}")
                .SendLogError("DeviceTemplate niet gevonden - ModelId: {ModelId}", modelId);
            throw new KeyNotFoundException($"DeviceTemplate with ModelId '{modelId}' not found.");
        }

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
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Template gekopieerd naar device {newName}")
            .SendLogInformation("CopyModelTemplateAsync voltooid - NewDeviceId: {DeviceId}, Aantal reports: {Count}", newDevice.Id, templateReports.Count);
    }

    // ----------------------------------- //
    // Creates a new device template entry //
    // ----------------------------------- //
    public async Task<DeviceTemplate> NewDvTemplateEntryAsync(string modelId, string name)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Nieuwe template aanmaken: {name}")
            .SendLogInformation("Start NewDvTemplateEntryAsync - ModelId: {ModelId}, Name: {Name}", modelId, name);

        var template = new DeviceTemplate
        {
            ModelId = modelId,
            Name = name,
            NumberOfActive = 1,
        };

        await repo.CreateAsync(template);

        logger
            .WithFacilicomContext(friendlyMessage: $"Template aangemaakt: {name}")
            .SendLogInformation("NewDvTemplateEntryAsync voltooid - TemplateId: {TemplateId}", template.Id);
            
        Console.WriteLine($"Template created: {name} ({modelId}) with ID {template.Id}");
        return template;
    }

    // ------------------------------------------------------------- //
    // Checks if a model template exists; copies template if present //
    // ------------------------------------------------------------- //
    public async Task<string> ModelPresentAsync(string modelId, string address)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Controleren template voor model {modelId}")
            .SendLogInformation("Start ModelPresentAsync - ModelId: {ModelId}, Address: {Address}", modelId, address);

        var templateExists = await repo.Query<DeviceTemplate>()
            .AnyAsync(d => d.ModelId == modelId);

        Console.WriteLine(templateExists ? "Model already present" : "Model not yet present");
        string type;
        if (templateExists)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Template bestaat, wordt gekopieerd")
                .SendLogInformation("Template gevonden, start kopiëren");
            type = "templateExist";
            await CopyModelTemplateAsync(modelId, address);
        }
        else
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Template bestaat niet")
                .SendLogInformation("Template niet gevonden");
            type = "templateNotExist";
        }

        logger
            .WithFacilicomContext(friendlyMessage: $"Resultaat: {type}")
            .SendLogInformation("ModelPresentAsync voltooid - Type: {Type}", type);

        return type;
    }
}