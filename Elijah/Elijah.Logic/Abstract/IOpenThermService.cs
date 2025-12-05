using System.Text.Json;

namespace Elijah.Logic.Abstract;

// ---------------------------------------------- //
// Interface for OpenTherm gateway communication  //
// ---------------------------------------------- //
public interface IOpenThermService
{
    // ------------------------------------------------------ //
    // Initializes ESP serial connection and waits for ready  //
    // ------------------------------------------------------ //
    Task EspConnect();

    // -------------------------------- //
    // Sends configuration data to ESP  //
    // -------------------------------- //
    Task SendConfigToEspAsync();

    // ------------------------------------------- //
    // Continuously listens for messages from ESP  //
    // ------------------------------------------- //
    IAsyncEnumerable<IncomingMessage> ListenForIncomingMessagesAsync(CancellationToken cancellationToken = default);

    // -------------------------------- //
    // Sends a parameter update to ESP  //
    // -------------------------------- //
    Task SendParameterAsync(string id, object value);

    // ------------------------------------------------ //
    // Updates or creates OpenTherm config in database  //
    // ------------------------------------------------ //
    Task UpdateOrCreateConfigAsync(int id, int intervalSec, float threshold);
    
    // ---------------------------------------- //
    // Represents an incoming message from ESP  //
    // ---------------------------------------- //
    public record IncomingMessage(string Id, JsonElement Value);
}