// ------------------------------------------------------------ //
// MQTT Broker configuration settings                          //
// Used for establishing connection to Zigbee2MQTT             //
// ------------------------------------------------------------ //

namespace Elijah.Domain.Config;

public class MqttConfig
{
    // MQTT broker hostname or IP address
    public string HostName { get; set; }

    // MQTT broker port 
    public int Port { get; set; }

    // Unique client identifier for MQTT connection
    public string ClientId { get; set; }
}