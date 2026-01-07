using Elijah.Data.Repository;
using Elijah.Domain.Entities;
using Elijah.Logic.Abstract;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FacilicomLogManager.Extensions;

namespace Elijah.Logic.Concrete;


// ---------------------------------------------------------------- //
// Service for managing device data filters                         //
// Controls which payload properties are processed for each device  //
// ---------------------------------------------------------------- //
public class DeviceFilterService(
    IZigbeeRepository repo, 
    IDeviceService deviceService,
    ILogger<DeviceFilterService> logger)
    : IDeviceFilterService
{
    // --------------------------------------------------------------------------------- //
    // Returns a list of all filter that can be applied to the sent payload of a device  //
    // --------------------------------------------------------------------------------- //
    public async Task<List<string>?> QueryDataFilterAsync(string address)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Ophalen data filters voor device {address}")
            .SendLogInformation("Start QueryDataFilterAsync - Address: {Address}", address);

        if (string.IsNullOrWhiteSpace(address))
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Ongeldig device adres")
                .SendLogWarning("Leeg device adres in QueryDataFilterAsync");
            return new List<string>();
        }

        var filters = await repo.Query<DeviceFilter>()
            .Where(filter => filter.Device.Address == address)
            .Select(filter => filter.FilterValue)
            .ToListAsync();

        logger
            .WithFacilicomContext(friendlyMessage: $"Data filters opgehaald voor {address}")
            .SendLogInformation("QueryDataFilterAsync voltooid - Aantal filters: {Count}", filters.Count);

        return filters;
    }

    // ------------------------------------------------------------ //
    // Creates new filter and relates it back to an address/device  //
    // ------------------------------------------------------------ //
    public async Task NewFilterEntryAsync(string address, string filterValue)
    {
        logger
            .WithFacilicomContext(friendlyMessage: $"Nieuwe filter entry voor device {address}")
            .SendLogInformation("Start NewFilterEntryAsync - Address: {Address}, FilterValue: {FilterValue}", address, filterValue);

        var device = await deviceService.GetDeviceByAddressAsync(address);
        if (device == null)
        {
            logger
                .WithFacilicomContext(friendlyMessage: $"Device {address} niet gevonden")
                .SendLogWarning("Device niet gevonden bij NewFilterEntryAsync - Address: {Address}", address);
            return; 
        }

        await repo.CreateAsync(
            new DeviceFilter
            {
                DeviceId = device.Id,
                FilterValue = filterValue
            });

        await repo.SaveChangesAsync();
        
        logger
            .WithFacilicomContext(friendlyMessage: $"Filter entry opgeslagen voor {address}")
            .SendLogInformation("NewFilterEntryAsync voltooid - DeviceId: {DeviceId}, FilterValue: {FilterValue}", device.Id, filterValue);
    }
}