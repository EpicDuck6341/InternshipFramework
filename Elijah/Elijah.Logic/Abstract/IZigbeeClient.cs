namespace Elijah.Logic.Abstract
{
    // ----------------------------- //
    // Main Zigbee client interface  //
    // ----------------------------- //
    public interface IZigbeeClient
    {
        // ------------------------ //
        // Connects to MQTT broker  //
        // ------------------------ //
        Task ConnectToMqtt();

        // ------------------------------ //
        // Sends reporting configurations //
        // ------------------------------ //
        Task SendReportConfig();

        // -------------------- //
        // Sends device options //
        // -------------------- //
        Task SendDeviceOptions();

        // ------------------------------------------ //
        // Enables joining and processes new devices  //
        // ------------------------------------------ //
        Task AllowJoinAndListen(int seconds);

        // ------------------------- //
        // Removes a device by name  //
        // ------------------------- //
        Task RemoveDevice(string name);

        // -------------------------- //
        // Starts message processing  //
        // -------------------------- //
        void StartProcessingMessages();

        // ------------------------------ //
        // Gets device reporting details  //
        // ------------------------------ //
        Task GetDeviceDetails(string address, string modelId);

        // --------------------------- //
        // Gets device option details  //
        // --------------------------- //
        Task GetOptionDetails(string address, string model, List<string> readableProps, List<string> description);
        
        // --------------------------------- //
        // Subscribes to all active devices  //
        // --------------------------------- //
        Task SubscribeToAll();
    }
}