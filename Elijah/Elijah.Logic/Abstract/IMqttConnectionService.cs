using MQTTnet;

namespace Elijah.Logic.Abstract;

public interface IMqttConnectionService
{
    Task ConnectAsync();
    Task DisconnectAsync();
    IMqttClient Client { get; }
}
