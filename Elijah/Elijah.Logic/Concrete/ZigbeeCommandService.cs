using Elijah.Logic.Abstract;
using Microsoft.Azure.Devices.Client;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FacilicomLogManager.Extensions;

namespace Elijah.Logic.Concrete;

public class ZigbeeCommandService(
    IZigbeeClient zigbeeClient,
    ModuleClient moduleClient,
    IServiceScopeFactory scopeFactory,
    ILogger<ZigbeeCommandService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"ZigbeeCommandService starten")
            .SendLogInformation("ZigbeeCommandService StartAsync");

        if (moduleClient == null)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"ModuleClient niet beschikbaar")
                .SendLogWarning("ModuleClient not available - running in local dev mode");
            return Task.CompletedTask;
        }

        // Register all direct-method handlers
        moduleClient.SetMethodHandlerAsync("ConnectToMqtt", HandleConnectToMqtt, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("SendReportConfig", HandleSendReportConfig, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("SendDeviceOptions", HandleSendDeviceOptions, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("AllowJoinAndListen", HandleAllowJoin, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("RemoveDevice", HandleRemoveDevice, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("GetDeviceDetails", HandleGetDeviceDetails, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("GetOptionDetails", HandleGetOptionDetails, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("SubscribeToAll", HandleSubscribeToAll, null, cancellationToken);
        moduleClient.SetMethodHandlerAsync("GetDeviceList", HandleGetDeviceList, null, cancellationToken);

        logger
            .WithFacilicomContext(friendlyMessage: $"10 Azure direct-method handlers geregistreerd")
            .SendLogInformation("Registered 10 Azure direct-method handlers");
        return Task.CompletedTask;
    }
    
    private async Task<MethodResponse> HandleConnectToMqtt(MethodRequest req, object ctx)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"ConnectToMqtt direct method")
            .SendLogInformation("HandleConnectToMqtt called");
        try { await zigbeeClient.ConnectToMqtt(); return Ok(); }
        catch (Exception ex) 
        { 
            logger
                .WithFacilicomContext(friendlyMessage: $"Fout in ConnectToMqtt")
                .SendLogError(ex, "HandleConnectToMqtt fout - Message: {Message}", ex.Message);
            return Error(ex); 
        }
    }

    private async Task<MethodResponse> HandleSendReportConfig(MethodRequest req, object ctx)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"SendReportConfig direct method")
            .SendLogInformation("HandleSendReportConfig called");
        try { await zigbeeClient.SendReportConfig(); return Ok(); }
        catch (Exception ex) 
        { 
            logger
                .WithFacilicomContext(friendlyMessage: $"Fout in SendReportConfig")
                .SendLogError(ex, "HandleSendReportConfig fout - Message: {Message}", ex.Message);
            return Error(ex); 
        }
    }

    private async Task<MethodResponse> HandleSendDeviceOptions(MethodRequest req, object ctx)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"SendDeviceOptions direct method")
            .SendLogInformation("HandleSendDeviceOptions called");
        try { await zigbeeClient.SendDeviceOptions(); return Ok(); }
        catch (Exception ex) 
        { 
            logger
                .WithFacilicomContext(friendlyMessage: $"Fout in SendDeviceOptions")
                .SendLogError(ex, "HandleSendDeviceOptions fout - Message: {Message}", ex.Message);
            return Error(ex); 
        }
    }

    private async Task<MethodResponse> HandleAllowJoin(MethodRequest req, object ctx)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"AllowJoin direct method")
            .SendLogInformation("HandleAllowJoin called");
        try
        {
            var data = JsonSerializer.Deserialize<AllowJoinRequest>(req.DataAsJson);
            await zigbeeClient.AllowJoinAndListen(data.Seconds);
            return new MethodResponse(Encoding.UTF8.GetBytes($"{{\"status\":\"join_enabled\",\"seconds\":{data.Seconds}}}"), 200);
        }
        catch (Exception ex) 
        { 
            logger
                .WithFacilicomContext(friendlyMessage: $"Fout in AllowJoin")
                .SendLogError(ex, "HandleAllowJoin fout - Message: {Message}", ex.Message);
            return Error(ex); 
        }
    }

    private async Task<MethodResponse> HandleRemoveDevice(MethodRequest req, object ctx)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"RemoveDevice direct method")
            .SendLogInformation("HandleRemoveDevice called");
        try
        {
            var data = JsonSerializer.Deserialize<RemoveDeviceRequest>(req.DataAsJson);
            await zigbeeClient.RemoveDevice(data.DeviceName);
            return Ok();
        }
        catch (Exception ex) 
        { 
            logger
                .WithFacilicomContext(friendlyMessage: $"Fout in RemoveDevice")
                .SendLogError(ex, "HandleRemoveDevice fout - Message: {Message}", ex.Message);
            return Error(ex); 
        }
    }


    private async Task<MethodResponse> HandleGetDeviceDetails(MethodRequest req, object ctx)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"GetDeviceDetails direct method")
            .SendLogInformation("HandleGetDeviceDetails called");
        try
        {
            var data = JsonSerializer.Deserialize<GetDeviceDetailsRequest>(req.DataAsJson);
            await zigbeeClient.GetDeviceDetails(data.Address, data.ModelId);
            return Ok();
        }
        catch (Exception ex) 
        { 
            logger
                .WithFacilicomContext(friendlyMessage: $"Fout in GetDeviceDetails")
                .SendLogError(ex, "HandleGetDeviceDetails fout - Message: {Message}", ex.Message);
            return Error(ex); 
        }
    }

    private async Task<MethodResponse> HandleGetOptionDetails(MethodRequest req, object ctx)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"GetOptionDetails direct method")
            .SendLogInformation("HandleGetOptionDetails called");
        try
        {
            var data = JsonSerializer.Deserialize<GetOptionDetailsRequest>(req.DataAsJson);
            await zigbeeClient.GetOptionDetails(data.Address, data.Model, data.ReadableProps, data.Description);
            return Ok();
        }
        catch (Exception ex) 
        { 
            logger
                .WithFacilicomContext(friendlyMessage: $"Fout in GetOptionDetails")
                .SendLogError(ex, "HandleGetOptionDetails fout - Message: {Message}", ex.Message);
            return Error(ex); 
        }
    }

    private async Task<MethodResponse> HandleSubscribeToAll(MethodRequest req, object ctx)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"SubscribeToAll direct method")
            .SendLogInformation("HandleSubscribeToAll called");
        try { await zigbeeClient.SubscribeToAll(); return Ok(); }
        catch (Exception ex) 
        { 
            logger
                .WithFacilicomContext(friendlyMessage: $"Fout in SubscribeToAll")
                .SendLogError(ex, "HandleSubscribeToAll fout - Message: {Message}", ex.Message);
            return Error(ex); 
        }
    }

    private async Task<MethodResponse> HandleGetDeviceList(MethodRequest req, object ctx)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"GetDeviceList direct method")
            .SendLogInformation("HandleGetDeviceList called");
        try
        {
            using var scope = scopeFactory.CreateScope();
            var deviceService = scope.ServiceProvider.GetRequiredService<IDeviceService>();
            var devices = await deviceService.GetActiveAddressesAsync();
            var json = JsonSerializer.Serialize(new { devices });
            
            logger
                .WithFacilicomContext(friendlyMessage: $"Device lijst opgehaald: {devices.Count}")
                .SendLogInformation("Device lijst geretourneerd - Count: {Count}", devices.Count);
            return new MethodResponse(Encoding.UTF8.GetBytes(json), 200);
        }
        catch (Exception ex) 
        { 
            logger
                .WithFacilicomContext(friendlyMessage: $"Fout in GetDeviceList")
                .SendLogError(ex, "HandleGetDeviceList fout - Message: {Message}", ex.Message);
            return Error(ex); 
        }
    }

    // Helpers
    private static MethodResponse Ok() 
    {
        return new(Encoding.UTF8.GetBytes("{\"status\":\"success\"}"), 200);
    }
    
    private static MethodResponse Error(Exception ex) 
    {
        return new(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { status = "error", message = ex.Message })), 500);
    }

    public Task StopAsync(CancellationToken cancellationToken) 
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"ZigbeeCommandService gestopt")
            .SendLogInformation("ZigbeeCommandService StopAsync");
        return Task.CompletedTask;
    }

    // Request DTOs
    private record AllowJoinRequest(int Seconds);
    private record RemoveDeviceRequest(string DeviceName);
    private record GetDeviceDetailsRequest(string Address, string ModelId);
    private record GetOptionDetailsRequest(string Address, string Model, List<string> ReadableProps, List<string> Description);
}