using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Elijah.Logic.Abstract;

public interface IOpenThermService
{
    Task ESPConnect();
    Task SendConfigToEspAsync();
    IAsyncEnumerable<IncomingMessage> ListenForIncomingMessagesAsync(CancellationToken cancellationToken = default);
    Task SendParameterAsync(string id, object value);
    Task UpdateOrCreateConfigAsync(int id, int intervalSec, float threshold);
    
    public record IncomingMessage(string ID, JsonElement VALUE);
}