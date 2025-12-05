using MQTTnet;

namespace Elijah.Logic.Abstract;

// ----------------------------------------- //
// Interface for MQTT connection management  //
// ----------------------------------------- //
public interface IMqttConnectionService
{
    // ------------------------------------- //
    // Establishes connection to MQTT broker //
    // ------------------------------------- //
    Task ConnectAsync();

    // ----------------------------- //
    // Disconnects from MQTT broker  //
    // ----------------------------- //
    Task DisconnectAsync();

    // --------------------------------------------- //
    // Provides access to the underlying MQTT client  //
    // --------------------------------------------- //
    IMqttClient Client { get; }
}