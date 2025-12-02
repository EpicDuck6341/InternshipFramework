using Elijah.Domain.Entities;
using Elijah.Domain.Models;

namespace Elijah.Logic.Abstract;

public interface ISendService
{
    Task SendReportConfigAsync(List<ReportConfig> configs);
    Task SendDeviceOptionsAsync(List<ChangedOption> opts);
    Task PermitJoinAsync(int seconds);
    Task CloseJoinAsync();
    Task RemoveDeviceAsync(string address);
    Task SetBrightnessAsync(string address, int brightness);
}