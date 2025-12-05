// ----------------------------------------------------------- //
// Entity storing option data for devices that didn't respond  //
// in time during initial setup, to be processed later         //
// ----------------------------------------------------------- //

namespace Elijah.Domain.Models;

public class PendingOptionData
{
    // Device identifier (IEEE address)
    public required string Address { get; init; }

    // Device model identifier
    public required string Model { get; set; }

    // List of readable property names from device exposes
    public required List<string> ReadableProps { get; init; }

    // List of human-readable descriptions matching properties
    public required List<string> Descriptions { get; init; }
}