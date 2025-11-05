using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Elijah.Domain.Entities;

public class DeviceFilter
{
    [Key]
    public int Id { get; set; }
    public string Address { get; set; } // Foreign Key
    public string Type { get; set; }
    public string FilterValue { get; set; }
    public bool Active { get; set; }

    [ForeignKey(nameof(Address))]
    public virtual Device Device { get; set; }
}