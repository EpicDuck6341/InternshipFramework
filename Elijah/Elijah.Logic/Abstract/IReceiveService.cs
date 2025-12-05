namespace Elijah.Logic.Abstract;

// ---------------------------------------------- //
// Interface for MQTT message reception handling  //
// ---------------------------------------------- //
public interface IReceiveService
{
    // ----------------------------------- //
    // Starts the message processing loop  //
    // ----------------------------------- //
    void StartMessageLoop();
}