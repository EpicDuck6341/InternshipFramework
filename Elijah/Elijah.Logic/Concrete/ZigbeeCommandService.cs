using Elijah.Logic.Abstract;
using Microsoft.Azure.Devices.Client;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Elijah.Logic.Concrete;

public class ZigbeeCommandService(
    IZigbeeClient zigbeeClient,
    ModuleClient moduleClient,
    IServiceScopeFactory scopeFactory) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (moduleClient == null)
        {
            Console.WriteLine("ModuleClient not available - running in local dev mode");
            return Task.CompletedTask;
        }

        // Register all direct-method handlers
        moduleClient.SetMethodHandlerAsync("ConnectToMqtt", HandleConnectToMqtt, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("SendReportConfig", HandleSendReportConfig, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("SendDeviceOptions", HandleSendDeviceOptions, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("AllowJoinAndListen", HandleAllowJoin, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("RemoveDevice", HandleRemoveDevice, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("StartProcessingMessages", HandleStartProcessing, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("GetDeviceDetails", HandleGetDeviceDetails, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("GetOptionDetails", HandleGetOptionDetails, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("SubscribeToAll", HandleSubscribeToAll, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("GetDeviceList", HandleGetDeviceList, null, cancellationToken);

        Console.WriteLine("Registered 10 Azure direct-method handlers");
        return Task.CompletedTask;
    }

    // Handlers
    private async Task<MethodResponse> HandleConnectToMqtt(MethodRequest req, object ctx)
    {
        try { await zigbeeClient.ConnectToMqtt(); return Ok(); }
        catch (Exception ex) { return Error(ex); }
    }

    private async Task<MethodResponse> HandleSendReportConfig(MethodRequest req, object ctx)
    {
        try { await zigbeeClient.SendReportConfig(); return Ok(); }
        catch (Exception ex) { return Error(ex); }
    }

    private async Task<MethodResponse> HandleSendDeviceOptions(MethodRequest req, object ctx)
    {
        try { await zigbeeClient.SendDeviceOptions(); return Ok(); }
        catch (Exception ex) { return Error(ex); }
    }

    private async Task<MethodResponse> HandleAllowJoin(MethodRequest req, object ctx)
    {
        try
        {
            var data = JsonSerializer.Deserialize<AllowJoinRequest>(req.DataAsJson);
            await zigbeeClient.AllowJoinAndListen(data.Seconds);
            return new MethodResponse(Encoding.UTF8.GetBytes($"{{\"status\":\"join_enabled\",\"seconds\":{data.Seconds}}}"), 200);
        }
        catch (Exception ex) { return Error(ex); }
    }

    private async Task<MethodResponse> HandleRemoveDevice(MethodRequest req, object ctx)
    {
        try
        {
            var data = JsonSerializer.Deserialize<RemoveDeviceRequest>(req.DataAsJson);
            await zigbeeClient.RemoveDevice(data.DeviceName);
            return Ok();
        }
        catch (Exception ex) { return Error(ex); }
    }

    private Task<MethodResponse> HandleStartProcessing(MethodRequest req, object ctx)
    {
        try { zigbeeClient.StartProcessingMessages(); return Task.FromResult(Ok()); }
        catch (Exception ex) { return Task.FromResult(Error(ex)); }
    }

    private async Task<MethodResponse> HandleGetDeviceDetails(MethodRequest req, object ctx)
    {
        try
        {
            var data = JsonSerializer.Deserialize<GetDeviceDetailsRequest>(req.DataAsJson);
            await zigbeeClient.GetDeviceDetails(data.Address, data.ModelId);
            return Ok();
        }
        catch (Exception ex) { return Error(ex); }
    }

    private async Task<MethodResponse> HandleGetOptionDetails(MethodRequest req, object ctx)
    {
        try
        {
            var data = JsonSerializer.Deserialize<GetOptionDetailsRequest>(req.DataAsJson);
            await zigbeeClient.GetOptionDetails(data.Address, data.Model, data.ReadableProps, data.Description);
            return Ok();
        }
        catch (Exception ex) { return Error(ex); }
    }

    private async Task<MethodResponse> HandleSubscribeToAll(MethodRequest req, object ctx)
    {
        try { await zigbeeClient.SubscribeToAll(); return Ok(); }
        catch (Exception ex) { return Error(ex); }
    }

    private async Task<MethodResponse> HandleGetDeviceList(MethodRequest req, object ctx)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
            var devices = await deviceService.GetActiveAddressesAsync();
            var json = JsonSerializer.Serialize(new { devices });
            return new MethodResponse(Encoding.UTF8.GetBytes(json), 200);
        }
        catch (Exception ex) { return Error(ex); }
    }

    // Helpers
    private static MethodResponse Ok() => new(Encoding.UTF8.GetBytes("{\"status\":\"success\"}"), 200);
    private static MethodResponse Error(Exception ex) => new(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { status = "error", message = ex.Message })), 500);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Request DTOs
    private record AllowJoinRequest(int Seconds);
    private record RemoveDeviceRequest(string DeviceName);
    private record GetDeviceDetailsRequest(string Address, string ModelId);
    private record GetOptionDetailsRequest(string Address, string Model, List<string> ReadableProps, List<string> Description);
}