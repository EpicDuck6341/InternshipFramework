using Elijah.Data;
using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;

namespace Elijah.Logic.Concrete;

public class ConfiguredReportingsService(ZigbeeRepository repo) : IConfiguredReportingsService
{
    

    public async Task<List<ReportConfig>> QueryReportIntervalAsync(string address, string table)
{
    List<ReportConfig> configList = new List<ReportConfig>();

    if (table == "A") // ConfiguredReportings
    {
        var configs = await repo.Query<ConfiguredReporting>()
                                .Where(r => r.Address == address)
                                .ToListAsync();

        foreach (var r in configs)
        {
            configList.Add(new ReportConfig(
                r.Address,
                r.Cluster,
                r.Attribute,
                r.MaximumReportInterval.ToString(),
                r.MinimumReportInterval.ToString(),
                r.ReportableChange,
                r.Endpoint
            ));
        }
    }
    else // ReportTemplate
    {
        var templates = await repo.Query<ReportTemplate>().ToListAsync();

        foreach (var r in templates)
        {
            configList.Add(new ReportConfig(
                null,
                r.Cluster,
                r.Attribute,
                r.MaximumReportInterval.ToString(),
                r.MinimumReportInterval.ToString(),
                r.ReportableChange,
                r.Endpoint
            ));
        }
    }

    return configList;
}

public async Task NewConfigRepEntryAsync(
    string tableName,
    string address,
    string modelID,
    string cluster,
    string attribute,
    string maxInterval,
    string minInterval,
    string reportableChange,
    string endpoint)
{
    tableName = tableName.ToLower();
    if (tableName == "reporttemplate")
    {
        repo.CreateAsync(new ReportTemplate
        {
            ModelId = modelID,
            Cluster = cluster,
            Attribute = attribute,
            MaximumReportInterval = maxInterval,
            MinimumReportInterval = minInterval,
            ReportableChange = reportableChange,
            Endpoint = endpoint
        });
    }
    else if (tableName == "configuredreportings")
    {
        repo.CreateAsync(new ConfiguredReporting
        {
            Address = address,
            Cluster = cluster,
            Attribute = attribute,
            MaximumReportInterval = maxInterval,
            MinimumReportInterval = minInterval,
            ReportableChange = reportableChange,
            Endpoint = endpoint
        });
    }
    else throw new ArgumentException("Invalid table name specified.");

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
    var report = await repo.Query<ConfiguredReporting>()
                           .FirstOrDefaultAsync(r =>
                               r.Address == address &&
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
    if (subscribedAddresses == null || subscribedAddresses.Count == 0)
        return new List<ReportConfig>();

    var changed = await repo.Query<ConfiguredReporting>()
                            .Where(r => r.Changed && subscribedAddresses.Contains(r.Address))
                            .ToListAsync();

    var configs = changed.Select(r => new ReportConfig(
        r.Address,
        r.Cluster,
        r.Attribute,
        r.MaximumReportInterval.ToString(),
        r.MinimumReportInterval.ToString(),
        r.ReportableChange,
        r.Endpoint
    )).ToList();

    foreach (var r in changed)
        r.Changed = false;

    await repo.SaveChangesAsync();
    return configs;
}

}