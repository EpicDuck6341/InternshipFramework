using Elijah.Domain.Entities;

namespace Elijah.Logic.Abstract;

public interface ISendService
{
    Task SendReportConfigAsync(List<ReportConfig> configs);
    Task SendDeviceOptionsAsync(List<ChangedOption> opts);
    Task PermitJoinAsync(int seconds);
    Task RemoveDeviceAsync(string address);
    Task SetBrightnessAsync(string address, int brightness);
}