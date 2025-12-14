using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Domain.Models;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;


// ---------------------------------------------------------- //
// Service for managing device reporting configurations       //
// Handles CRUD operations for ConfiguredReporting entities   //
// ---------------------------------------------------------- //
public class ConfiguredReportingsService(IZigbeeRepository repo, IDeviceService deviceService)
    : IConfiguredReportingsService
{
    // ------------------------------------------------------------ //
    // Helper: Query all ConfiguredReporting entries for an address //
    // ------------------------------------------------------------ //
    private IQueryable<ConfiguredReporting> QueryByAddress(string address)
    {
        return repo.Query<ConfiguredReporting>()
            .Include(r => r.Device)
            .Where(r => r.Device.Address == address);
    }

    // ---------------------------------------------- //
    // Helper: Convert ConfiguredReporting to DTO     //
    // ---------------------------------------------- //
    private static ReportConfig ToReportConfig(ConfiguredReporting r)
    {
        return new ReportConfig(
            r.Device.Address,
            r.Cluster,
            r.Attribute,
            r.MaximumReportInterval,
            r.MinimumReportInterval,
            r.ReportableChange,
            r.Endpoint
        );
    }


    // ------------------------------------------------------------ //
    // Creates a new ReportConfigEntry for a single device/address  //
    // ------------------------------------------------------------ //
    public async Task NewConfigRepEntryAsync(
        string address,
        string cluster,
        string attribute,
        string maxInterval,
        string minInterval,
        string reportableChange,
        string endpoint
    )
    {
        var device = await deviceService.GetDeviceByAddressAsync(address);
        if (device == null)
            return;

        await repo.CreateAsync(new ConfiguredReporting
        {
            DeviceId = device.Id,
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


    // ------------------------------------------------------------------------------ //
    // Adjusts a single config entry based on address, cluster, attribute and endpoint //
    // ------------------------------------------------------------------------------ //
    public async Task AdjustRepConfigAsync(
        string address,
        string cluster,
        string attribute,
        string maxInterval,
        string minInterval,
        string reportableChange,
        string endpoint
    )
    {
        var device = await deviceService.GetDeviceByAddressAsync(address, true);
        if (device == null)
            return;

        var report = await repo.Query<ConfiguredReporting>()
            .FirstOrDefaultAsync(r =>
                r.DeviceId == device.Id &&
                r.Cluster == cluster &&
                r.Attribute == attribute &&
                r.Endpoint == endpoint
            );

        if (report == null)
            return;

        report.MaximumReportInterval = maxInterval;
        report.MinimumReportInterval = minInterval;
        report.ReportableChange = reportableChange;
        report.Changed = true;

        await repo.SaveChangesAsync();
    }


    // ------------------------------------------------------------------------------------------------------- //
    // Returns a list of ReportConfigs for all reporting entries related to an address that have been changed   //
    // ------------------------------------------------------------------------------------------------------- //
    public async Task<List<ReportConfig>> GetChangedReportConfigsAsync(List<string> subscribedAddresses)
    {
        if (subscribedAddresses.Count == 0)
            return [];
        
        var changed = await repo.Query<ConfiguredReporting>()
            .Include(r => r.Device)
            .Where(r => r.Changed && subscribedAddresses.Contains(r.Device.Address))
            .ToListAsync();
        
        return changed.Select(ToReportConfig).ToList();
    }
    
    // ----------------------------------------------------------------------------------------- //
    // Used for setting the reporting time of a device to 0 for instant receival of its Options  //
    // ----------------------------------------------------------------------------------------- //
    public async Task<List<ReportConfig>> GetAllReportConfigsForAddressAsync(string deviceAddress)
    {
        if (string.IsNullOrWhiteSpace(deviceAddress))
            return new List<ReportConfig>();

   
        var configsForDevice = await repo.Query<ConfiguredReporting>()
            .Include(r => r.Device)
            .Where(r => r.Device.Address == deviceAddress)
            .ToListAsync();

        // Map to ReportConfig DTO
        var configs = configsForDevice.Select(r => new ReportConfig(
            r.Device.Address,
            r.Cluster,
            r.Attribute,
            "0",
            "0",
            "0",
            r.Endpoint
        )).ToList();

        return configs;
    }
    
    // -------------------------------------------------------------------------------- //
    // Returns a list of ReportConfigs for all reporting entries related to an address  //
    // -------------------------------------------------------------------------------- //
    public async Task<List<ReportConfig>> ConfigByAddress(string deviceAddress)
    {
        if (string.IsNullOrWhiteSpace(deviceAddress))
            return [];

        var configs = await QueryByAddress(deviceAddress).ToListAsync();
        return configs.Select(ToReportConfig).ToList();
    }
}
