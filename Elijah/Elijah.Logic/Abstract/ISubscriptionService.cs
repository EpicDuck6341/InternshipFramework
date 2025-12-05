namespace Elijah.Logic.Abstract;

// ------------------------------------------- //
// Interface for MQTT subscription management  //
// ------------------------------------------- //
public interface ISubscriptionService
{
    // ------------------------------ //
    // Subscribes to a single device  //
    // ------------------------------ //
    Task SubscribeAsync(string address);

    // --------------------------------- //
    // Subscribes to all active devices  //
    // --------------------------------- //
    Task SubscribeAllActiveDevicesAsync();
}