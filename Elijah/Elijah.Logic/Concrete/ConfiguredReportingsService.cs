using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class ConfiguredReportingsService(IZigbeeRepository repo,IDeviceService _device) : IConfiguredReportingsService
{
    

    public async Task<List<ReportConfig>> QueryReportIntervalAsync(string address)
    {
        
        var deviceId = await _device.AddressToIdAsync(address);
        if (deviceId == null) 
            return new List<ReportConfig>();

       
        var configs = await repo.Query<ConfiguredReporting>()
            .Where(r => r.DeviceId == deviceId)  
            .ToListAsync();

        
        return configs.Select(r => new ReportConfig(
            address,  
            r.Cluster,
            r.Attribute,
            r.MaximumReportInterval, 
            r.MinimumReportInterval,  
            r.ReportableChange,
            r.Endpoint
        )).ToList();
    }
    
    public async Task NewConfigRepEntryAsync(  // REMINDER: Fixed for new schema
        string address,     
        string cluster,
        string attribute,
        string maxInterval,
        string minInterval,
        string reportableChange,
        string endpoint)
    {
        
        var deviceId = await _device.AddressToIdAsync(address);
        if (deviceId == null)
            throw new Exception($"Device not found: {address}");

      
        await repo.CreateAsync(new ConfiguredReporting
        {
            DeviceId = (int)deviceId,
            Cluster = cluster,
            Attribute = attribute,
            MaximumReportInterval = maxInterval,
            MinimumReportInterval = minInterval,
            ReportableChange = reportableChange,
            Endpoint = endpoint,
            IsTemplate = true,  
            Changed = false
        });

        await repo.SaveChangesAsync();
    }

public async Task AdjustRepConfigAsync(
    string address,
    string cluster,
    string attribute,
    string maxInterval,
    string minInterval,
    string reportableChange,
    string endpoint)
{
    var deviceId = await _device.AddressToIdAsync(address);
    if (deviceId == null) return; 

    var report = await repo.Query<ConfiguredReporting>()
        .FirstOrDefaultAsync(r =>
            r.DeviceId == deviceId && 
            r.Cluster == cluster &&
            r.Attribute == attribute &&
            r.Endpoint == endpoint);

    if (report != null)
    {
        report.MaximumReportInterval = maxInterval;
        report.MinimumReportInterval = minInterval;
        report.ReportableChange = reportableChange;
        report.Changed = true;
        await repo.SaveChangesAsync();
    }
}

public async Task<List<ReportConfig>> GetChangedReportConfigsAsync(List<string> subscribedAddresses)
{
    if (subscribedAddresses == null || !subscribedAddresses.Any())
        return new List<ReportConfig>();

  
    var changed = await repo.Query<ConfiguredReporting>()
        .Include(r => r.Device)
        .Where(r => r.Changed)
        .Where(r => subscribedAddresses.Contains(r.Device.Address)) 
        .ToListAsync();

 
    var configs = changed.Select(r => new ReportConfig(
        r.Device.Address,
        r.Cluster,
        r.Attribute,
        r.MaximumReportInterval,
        r.MinimumReportInterval,
        r.ReportableChange,
        r.Endpoint
    )).ToList();

   
    foreach (var r in changed)
        r.Changed = false;

    await repo.SaveChangesAsync();
    return configs;
}

}