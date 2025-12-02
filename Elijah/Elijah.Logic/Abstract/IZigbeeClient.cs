using System.Collections.Generic;
using System.Threading.Tasks;

namespace Elijah.Logic.Abstract
{
    public interface IZigbeeClient
    {
        
        Task ConnectToMqtt();
        Task SendReportConfig();
        Task SendDeviceOptions();
        Task AllowJoinAndListen(int seconds);
        Task RemoveDevice(string name);
        void StartProcessingMessages();
        Task GetDeviceDetails(string address, string modelID);
        Task GetOptionDetails(string address, string model, List<string> readableProps, List<string> description);
        
        Task subscribeToAll();
    }
}