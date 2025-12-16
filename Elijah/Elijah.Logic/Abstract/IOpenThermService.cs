using System.Text.Json;

namespace Elijah.Logic.Abstract;

// ---------------------------------------------- //
// Interface for OpenTherm gateway communication  //
// ---------------------------------------------- //
public interface IOpenThermService
{
    

    // -------------------------------- //
    // Sends configuration data to ESP  //
    // -------------------------------- //
    Task SendConfigToEspAsync(CancellationToken cancellationToken = default);

    // ------------------------------------------- //
    // Continuously listens for messages from ESP  //
    // ------------------------------------------- //
    IAsyncEnumerable<IncomingMessage> ListenForIncomingMessagesAsync(CancellationToken cancellationToken = default);

    // -------------------------------- //
    // Sends a parameter update to ESP  //
    // -------------------------------- //
    Task SendParameterAsync(string id, object value, CancellationToken cancellationToken = default);


    // ------------------------------------------------ //
    // Updates or creates OpenTherm config in database  //
    // ------------------------------------------------ //
    
    // ---------------------------------------- //
    // Represents an incoming message from ESP  //
    // ---------------------------------------- //
    public record IncomingMessage(string Id, JsonElement Value);

    Task UpdateOrCreateConfigAsync(int id, int intervalSec, float threshold,
        CancellationToken cancellationToken = default);

}