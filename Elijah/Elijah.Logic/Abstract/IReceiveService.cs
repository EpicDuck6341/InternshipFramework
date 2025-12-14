using Microsoft.Extensions.Hosting;

namespace Elijah.Logic.Abstract;

// ---------------------------------------------- //
// Interface for MQTT message reception handling  //
// ---------------------------------------------- //
public interface IReceiveService :IHostedService
{
    // ----------------------------------- //
    // Starts the message processing loop  //
    // ----------------------------------- //
    // void StartMessageLoop();
}