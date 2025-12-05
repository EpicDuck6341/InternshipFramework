using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Elijah.Domain.Entities.Abstract;

// ---------------------------------------------------- //
// Entity defining which payload properties to process  //
// Filters sensor data to only relevant keys            //
// ---------------------------------------------------- //

namespace Elijah.Domain.Entities;

[Table("DeviceFilter")]
public class DeviceFilter : BaseType
{
    [Key]
    public int Id { get; set; }

    // JSON property key to filter (e.g., "temperature", "humidity")
    public string FilterValue { get; set; }
    
    // Foreign key linking to Device entity
    public int DeviceId { get; set; }

    // Navigation property to parent Device
    [ForeignKey(nameof(DeviceId))]
    public Device Device { get; set; }
}