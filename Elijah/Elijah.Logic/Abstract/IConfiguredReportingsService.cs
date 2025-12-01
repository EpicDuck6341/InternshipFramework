using Elijah.Domain.Models;

namespace Elijah.Logic.Abstract;

public interface IConfiguredReportingsService
{
    Task NewConfigRepEntryAsync(
        string address,
        string cluster,
        string attribute,
        string maxInterval,
        string minInterval,
        string reportableChange,
        string endpoint
    );
    Task AdjustRepConfigAsync(
        string address,
        string cluster,
        string attribute,
        string maxInterval,
        string minInterval,
        string reportableChange,
        string endpoint
    );
    Task<List<ReportConfig>> GetChangedReportConfigsAsync(List<string> subscribedAddresses);
}
