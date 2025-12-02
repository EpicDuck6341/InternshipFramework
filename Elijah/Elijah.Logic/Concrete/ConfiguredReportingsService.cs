using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Domain.Models;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class ConfiguredReportingsService(IZigbeeRepository repo, IDeviceService deviceService)
    : IConfiguredReportingsService
{
    public async Task<List<ReportConfig>> QueryReportIntervalAsync(string address)
    {
        var device = await deviceService.GetDeviceByAdressAsync(address, true);
        if (device == null)
            return [];

        var configs = await repo.Query<ConfiguredReporting>()
            .Where(r => r.DeviceId == device.Id)
            .ToListAsync();

        return configs
            .Select(r => new ReportConfig(
                address,
                r.Cluster,
                r.Attribute,
                r.MaximumReportInterval,
                r.MinimumReportInterval,
                r.ReportableChange,
                r.Endpoint
            ))
            .ToList();
    }

    public async Task NewConfigRepEntryAsync( // REMINDER: Fixed for new schema
        string address,
        string cluster,
        string attribute,
        string maxInterval,
        string minInterval,
        string reportableChange,
        string endpoint
    )
    {
        var device = await deviceService.GetDeviceByAdressAsync(address);

        await repo.CreateAsync(
            new ConfiguredReporting
            {
                DeviceId = device!.Id,
                Cluster = cluster,
                Attribute = attribute,
                MaximumReportInterval = maxInterval,
                MinimumReportInterval = minInterval,
                ReportableChange = reportableChange,
                Endpoint = endpoint,
                IsTemplate = true,
                Changed = false,
            }
        );

        await repo.SaveChangesAsync();
    }

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
        var device = await deviceService.GetDeviceByAdressAsync(address, true);
        if (device == null)
            return;

        var report = await repo.Query<ConfiguredReporting>()
            .FirstOrDefaultAsync(r =>
                r.DeviceId == device.Id
                && r.Cluster == cluster
                && r.Attribute == attribute
                && r.Endpoint == endpoint
            );

        if (report != null)
        {
            report.MaximumReportInterval = maxInterval;
            report.MinimumReportInterval = minInterval;
            report.ReportableChange = reportableChange;
            report.Changed = true;
            await repo.SaveChangesAsync();
        }
    }

    public async Task<List<ReportConfig>> GetChangedReportConfigsAsync(
        List<string> subscribedAddresses
    )
    {
        if (subscribedAddresses.Count > 0)
            return [];

        var changed = await repo.Query<ConfiguredReporting>()
            .Include(r => r.Device)
            .Where(r => r.Changed && subscribedAddresses.Contains(r.Device.Address))
            .ToListAsync();

        changed.ForEach(r => r.Changed = false);

        await repo.SaveChangesAsync();

        return changed
            .Select(r => new ReportConfig(
                r.Device.Address,
                r.Cluster,
                r.Attribute,
                r.MaximumReportInterval,
                r.MinimumReportInterval,
                r.ReportableChange,
                r.Endpoint
            ))
            .ToList();
    }


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


    public async Task<List<ReportConfig>> ConfigByAddress(string deviceAddress)
    {
        if (string.IsNullOrWhiteSpace(deviceAddress))
            return new List<ReportConfig>();

        // Query all configured reporting entries for this device
        var configsForDevice = await repo.Query<ConfiguredReporting>()
            .Include(r => r.Device)
            .Where(r => r.Device.Address == deviceAddress)
            .ToListAsync();

        // Map to ReportConfig DTO
        var configs = configsForDevice.Select(r => new ReportConfig(
            r.Device.Address,
            r.Cluster,
            r.Attribute,
            r.MaximumReportInterval,
            r.MinimumReportInterval,
            r.ReportableChange,
            r.Endpoint
        )).ToList();

        return configs;
    }
}