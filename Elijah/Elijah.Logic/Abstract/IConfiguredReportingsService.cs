using Elijah.Domain.Models;

namespace Elijah.Logic.Abstract;

public interface IConfiguredReportingsService
{
    Task NewConfigRepEntryAsync(
        string address,
        string? cluster,
        string? attribute,
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
    Task<List<ReportConfig>> GetAllReportConfigsForAddressAsync(string deviceAddress);//Set to 0 for fast reporting
    Task<List<ReportConfig>> ConfigByAddress(string deviceAddress);//used for setting config, even tho nothing has been changed.
}
