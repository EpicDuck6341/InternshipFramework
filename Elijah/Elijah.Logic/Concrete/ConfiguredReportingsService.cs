using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Domain.Models;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FacilicomLogManager.Extensions;

namespace Elijah.Logic.Concrete;


// ---------------------------------------------------------- //
// Service for managing device reporting configurations       //
// Handles CRUD operations for ConfiguredReporting entities   //
// ---------------------------------------------------------- //
public class ConfiguredReportingsService(
    IZigbeeRepository repo, 
    IDeviceService deviceService,
    ILogger<ConfiguredReportingsService> logger)
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
        logger
            .WithFacilicomContext(friendlyMessage: $"Nieuwe reporting config voor device {address}")
            .SendLogInformation("Start NewConfigRepEntryAsync - Address: {Address}, Cluster: {Cluster}, Attribute: {Attribute}, Endpoint: {Endpoint}", address, cluster, attribute, endpoint);

        var device = await deviceService.GetDeviceByAddressAsync(address);
        if (device == null)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Device {address} niet gevonden")
                .SendLogWarning("Device niet gevonden bij NewConfigRepEntryAsync - Address: {Address}", address);
            return;
        }

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
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Reporting config opgeslagen voor {address}")
            .SendLogInformation("NewConfigRepEntryAsync voltooid - DeviceId: {DeviceId}", device.Id);
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
        logger
            .WithFacilicomContext(friendlyMessage: $"Aanpassen reporting config voor device {address}")
            .SendLogInformation("Start AdjustRepConfigAsync - Address: {Address}, Cluster: {Cluster}, Attribute: {Attribute}", address, cluster, attribute);

        var device = await deviceService.GetDeviceByAddressAsync(address, true);
        if (device == null)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Device {address} niet gevonden")
                .SendLogWarning("Device niet gevonden bij AdjustRepConfigAsync - Address: {Address}", address);
            return;
        }

        var report = await repo.Query<ConfiguredReporting>()
            .FirstOrDefaultAsync(r =>
                r.DeviceId == device.Id &&
                r.Cluster == cluster &&
                r.Attribute == attribute &&
                r.Endpoint == endpoint
            );

        if (report == null)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Reporting config niet gevonden voor {address}")
                .SendLogWarning("Geen reporting config gevonden bij AdjustRepConfigAsync");
            return;
        }

        report.MaximumReportInterval = maxInterval;
        report.MinimumReportInterval = minInterval;
        report.ReportableChange = reportableChange;
        report.Changed = true;

        await repo.SaveChangesAsync();
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Reporting config aangepast voor {address}")
            .SendLogInformation("AdjustRepConfigAsync voltooid");
    }


    // ------------------------------------------------------------------------------------------------------- //
    // Returns a list of ReportConfigs for all reporting entries related to an address that have been changed   //
    // ------------------------------------------------------------------------------------------------------- //
    public async Task<List<ReportConfig>> GetChangedReportConfigsAsync(List<string> subscribedAddresses)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Ophalen gewijzigde reporting configs")
            .SendLogInformation("Start GetChangedReportConfigsAsync - A subscribed addresses: {Count}", subscribedAddresses.Count);

        if (subscribedAddresses.Count == 0)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Geen geabonneerde devices")
                .SendLogInformation("Geen subscribed addresses in GetChangedReportConfigsAsync");
            return [];
        }
        
        var changed = await repo.Query<ConfiguredReporting>()
            .Include(r => r.Device)
            .Where(r => r.Changed && subscribedAddresses.Contains(r.Device.Address))
            .ToListAsync();
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Gewijzigde configs opgehaald")
            .SendLogInformation("GetChangedReportConfigsAsync voltooid - Aantal configs: {Count}", changed.Count);
        
        return changed.Select(ToReportConfig).ToList();
    }
    
    // ----------------------------------------------------------------------------------------- //
    // Used for setting the reporting time of a device to 0 for instant receival of its Options  //
    // ----------------------------------------------------------------------------------------- //
    public async Task<List<ReportConfig>> GetAllReportConfigsForAddressAsync(string deviceAddress)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Ophalen alle reporting configs voor {deviceAddress}")
            .SendLogInformation("Start GetAllReportConfigsForAddressAsync - Address: {Address}", deviceAddress);

        if (string.IsNullOrWhiteSpace(deviceAddress))
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Ongeldig device adres")
                .SendLogWarning("Leeg device adres in GetAllReportConfigsForAddressAsync");
            return new List<ReportConfig>();
        }

   
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

        logger
            .WithFacilicomContext(friendlyMessage: $"Alle reporting configs opgehaald voor {deviceAddress}")
            .SendLogInformation("GetAllReportConfigsForAddressAsync voltooid - Aantal configs: {Count}", configs.Count);

        return configs;
    }
    
    // -------------------------------------------------------------------------------- //
    // Returns a list of ReportConfigs for all reporting entries related to an address  //
    // -------------------------------------------------------------------------------- //
    public async Task<List<ReportConfig>> ConfigByAddress(string deviceAddress)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Ophalen reporting configs voor {deviceAddress}")
            .SendLogInformation("Start ConfigByAddress - Address: {Address}", deviceAddress);

        if (string.IsNullOrWhiteSpace(deviceAddress))
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Ongeldig device adres")
                .SendLogWarning("Leeg device adres in ConfigByAddress");
            return [];
        }

        var configs = await QueryByAddress(deviceAddress).ToListAsync();
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Reporting configs opgehaald voor {deviceAddress}")
            .SendLogInformation("ConfigByAddress voltooid - Aantal configs: {Count}", configs.Count);
            
        return configs.Select(ToReportConfig).ToList();
    }
}