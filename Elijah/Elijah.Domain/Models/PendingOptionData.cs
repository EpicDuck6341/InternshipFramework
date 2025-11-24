namespace Elijah.Domain.Entities;

public class PendingOptionData
{
    public string Address { get; set; }
    public string Model { get; set; }
    public List<string> ReadableProps { get; set; }
    public List<string> Descriptions { get; set; }
}
