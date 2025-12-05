using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Elijah.Domain.Entities.Abstract;

// ----------------------------------------------------- //
// Entity storing readable/writable device options       //
// Represents device properties that can be queried/set  //
// ----------------------------------------------------- //

namespace Elijah.Domain.Entities;

[Table("Option")]
public class Option : BaseType
{
    [Key] 
    public int Id { get; set; }

    // Human-readable description of the option
    public string Description { get; set; }

    // Current value of the option
    public string CurrentValue { get; set; }

    // Property name (matches Zigbee property key)
    public string Property { get; set; }

    // Flag indicating if change has been processed/sent
    public bool IsProcessed { get; set; }
    
    // Foreign key linking to Device entity
    public int DeviceId { get; set; }
    
    // Navigation property to parent Device
    [ForeignKey(nameof(DeviceId))] 
    public Device Device { get; set; }
}