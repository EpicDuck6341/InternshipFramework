// ------------------------------------------------------------ //
// Model representing a changed device option                  //
// Used for tracking option modifications before transmission  //
// ------------------------------------------------------------ //

namespace Elijah.Domain.Models;

public class ChangedOption
{
    // Address of the device whose option changed
    public string? Address { get; init; }

    // Name of the property that was modified
    public string? Property { get; init; }

    // Current value after the change
    public string? CurrentValue { get; init; }
}