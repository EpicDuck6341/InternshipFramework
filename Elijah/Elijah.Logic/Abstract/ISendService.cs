using Elijah.Domain.Entities;
using Elijah.Domain.Models;

namespace Elijah.Logic.Abstract;

// --------------------------------------------------- //
// Interface for MQTT message transmission operations  //
// --------------------------------------------------- //
public interface ISendService
{
    // ------------------------------------------ //
    // Sends reporting configurations to devices  //
    // ------------------------------------------ //
    Task SendReportConfigAsync(List<ReportConfig> configs);

    // ----------------------------- //
    // Sends updated device options  //
    // ----------------------------- //
    Task SendDeviceOptionsAsync(List<ChangedOption> opts);

    // --------------------------------- //
    // Opens network for device joining  //
    // --------------------------------- //
    Task PermitJoinAsync(int seconds);

    // ---------------------------------- //
    // Closes network for device joining  //
    // ---------------------------------- //
    Task CloseJoinAsync();

    // ---------------------------------- //
    // Removes a device from the network  //
    // ---------------------------------- //
    Task RemoveDeviceAsync(string address);
}