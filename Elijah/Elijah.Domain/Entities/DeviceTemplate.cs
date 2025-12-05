using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Elijah.Domain.Entities.Abstract;

// -------------------------------------------------- //
// Template entity for device models                  //
// Used to autoconfigure new devices of known types  //
// -------------------------------------------------- //

namespace Elijah.Domain.Entities;

[Table("DeviceTemplate")]
public class DeviceTemplate : BaseType
{
    [Key]
    public int Id { get; set; }

    // Manufacturer model identifier (e.g., "TS0201")
    public string ModelId { get; set; }

    // Template name (base for auto-generated device names)
    [Required]
    public string Name { get; set; }
    
    // Number of active devices using this template
    public int NumberOfActive { get; set; }

    // Collection of devices instantiated from this template
    public ICollection<Device> Devices { get; set; }
}