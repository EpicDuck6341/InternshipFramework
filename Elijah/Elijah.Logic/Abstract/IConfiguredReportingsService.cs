using Elijah.Domain.Entities;

namespace Elijah.Logic.Abstract;

public interface IConfiguredReportingsService
{
    Task<List<ReportConfig>> QueryReportIntervalAsync(string address, string table);
    Task NewConfigRepEntryAsync(
        string tableName,
        string address,
        string modelID,
        string cluster,
        string attribute,
        string maxInterval,
        string minInterval,
        string reportableChange,
        string endpoint);
    Task AdjustRepConfigAsync(
        string address,
        string cluster,
        string attribute,
        string maxInterval,
        string minInterval,
        string reportableChange,
        string endpoint);
    Task<List<ReportConfig>> GetChangedReportConfigsAsync(List<string> subscribedAddresses);
}
