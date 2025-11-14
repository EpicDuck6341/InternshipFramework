namespace Elijah.Domain.Entities;

public class ChangedOption
{
    public string? Address { get; set; }
    public string? Property { get; set; }
    public string? CurrentValue { get; set; }

    // public ChangedOption(string? address, string? property, string? currentValue)
    // {
    //     Address = address;
    //     Property = property;
    //     CurrentValue = currentValue;
    // }
}