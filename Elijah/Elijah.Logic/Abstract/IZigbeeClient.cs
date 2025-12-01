namespace Elijah.Logic.Abstract
{
    public interface IZigbeeClient
    {
        bool IsReady { get; }

        Task ConnectToMqtt();
        Task SubscribeDevices();
        Task SubscribeAfterJoin(string address);
        Task SendReportConfig();
        Task SendDeviceOptions();
        Task AllowJoinAndListen(int seconds);
        Task RemoveDevice(string name);
        void StartProcessingMessages();

        Task GetDeviceDetails(string address, string modelId);
        Task GetOptionDetails(
            string address,
            string model,
            List<string> readableProps,
            List<string> description
        );

        Task ESPConnect();
        Task sendESPConfig(int brightness);
    }
}
