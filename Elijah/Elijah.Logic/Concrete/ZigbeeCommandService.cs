using Elijah.Logic.Abstract;
using Microsoft.Azure.Devices.Client;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Elijah.Logic.Concrete;

// -------------------------------------------------------- //
// Maps Azure IoT Hub direct methods to Zigbee operations  //
// -------------------------------------------------------- //
public class ZigbeeCommandService(
    IZigbeeClient zigbeeClient,
    ModuleClient moduleClient,
    IServiceScopeFactory scopeFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (moduleClient == null)
        {
            Console.WriteLine("⚠ModuleClient not available - running in local dev mode (no Azure commands)");
            return Task.CompletedTask;
        }

        // Register direct method handlers
        moduleClient.SetMethodHandlerAsync("AllowDeviceJoin", HandleAllowJoin, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("RemoveDevice", HandleRemoveDevice, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("SendDeviceOptions", HandleSendOptions, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("GetDeviceList", HandleGetDeviceList, null, cancellationToken);

        Console.WriteLine("Registered Azure direct method handlers");
        return Task.CompletedTask;
    }

    private async Task<MethodResponse> HandleAllowJoin(MethodRequest request, object context)
    {
        try
        {
            var data = JsonSerializer.Deserialize<JoinRequest>(request.DataAsJson);
            await zigbeeClient.AllowJoinAndListen(data.Seconds);
            return new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"success\"}"), 200);
        }
        catch (Exception ex)
        {
            var error = JsonSerializer.Serialize(new { status = "error", message = ex.Message });
            return new MethodResponse(Encoding.UTF8.GetBytes(error), 500);
        }
    }

    private async Task<MethodResponse> HandleRemoveDevice(MethodRequest request, object context)
    {
        try
        {
            var data = JsonSerializer.Deserialize<RemoveRequest>(request.DataAsJson);
            await zigbeeClient.RemoveDevice(data.DeviceName);
            return new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"success\"}"), 200);
        }
        catch (Exception ex)
        {
            var error = JsonSerializer.Serialize(new { status = "error", message = ex.Message });
            return new MethodResponse(Encoding.UTF8.GetBytes(error), 500);
        }
    }

    private async Task<MethodResponse> HandleSendOptions(MethodRequest request, object context)
    {
        try
        {
            await zigbeeClient.SendDeviceOptions();
            return new MethodResponse(Encoding.UTF8.GetBytes("{\"status\":\"success\"}"), 200);
        }
        catch (Exception ex)
        {
            var error = JsonSerializer.Serialize(new { status = "error", message = ex.Message });
            return new MethodResponse(Encoding.UTF8.GetBytes(error), 500);
        }
    }

    private async Task<MethodResponse> HandleGetDeviceList(MethodRequest request, object context)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
            var devices = await deviceService.GetActiveAddressesAsync();

            var response = JsonSerializer.Serialize(new { devices });
            return new MethodResponse(Encoding.UTF8.GetBytes(response), 200);
        }
        catch (Exception ex)
        {
            var error = JsonSerializer.Serialize(new { status = "error", message = ex.Message });
            return new MethodResponse(Encoding.UTF8.GetBytes(error), 500);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private record JoinRequest(int Seconds);

    private record RemoveRequest(string DeviceName);
}