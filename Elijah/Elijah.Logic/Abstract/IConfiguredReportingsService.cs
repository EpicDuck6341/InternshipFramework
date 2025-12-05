using Elijah.Domain.Models;

namespace Elijah.Logic.Abstract;

// ------------------------------------------------------------ //
// Interface for configured reporting management               //
// ------------------------------------------------------------ //
public interface IConfiguredReportingsService
{
    // ------------------------------------------------------------ //
    // Creates a new reporting configuration entry                 //
    // ------------------------------------------------------------ //
    Task NewConfigRepEntryAsync(
        string address,
        string cluster,
        string attribute,
        string maxInterval,
        string minInterval,
        string reportableChange,
        string endpoint
    );

    // ------------------------------------------------------------ //
    // Updates an existing reporting configuration                 //
    // ------------------------------------------------------------ //
    Task AdjustRepConfigAsync(
        string address,
        string cluster,
        string attribute,
        string maxInterval,
        string minInterval,
        string reportableChange,
        string endpoint
    );

    // ------------------------------------------------------------ //
    // Gets all changed configurations for subscribed devices      //
    // ------------------------------------------------------------ //
    Task<List<ReportConfig>> GetChangedReportConfigsAsync(List<string> subscribedAddresses);

    // ------------------------------------------------------------ //
    // Gets all configs for instant reporting (intervals set to 0) //
    // ------------------------------------------------------------ //
    Task<List<ReportConfig>> GetAllReportConfigsForAddressAsync(string deviceAddress);

    // ------------------------------------------------ //
    // Gets all configurations for a specific address   //
    // ------------------------------------------------ //
    Task<List<ReportConfig>> ConfigByAddress(string deviceAddress);
}