using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Elijah.Domain.Entities;

[Table("Devices", Schema = "dbo")]
public class Device
{
    [Key]
    public string Address { get; set; }
    public string ModelId { get; set; } // Foreign Key
    public string Name { get; set; }
    public bool Subscribed { get; set; }
    public bool Active { get; set; }

    [ForeignKey(nameof(ModelId))]
    public virtual DeviceTemplate DeviceTemplate { get; set; }

    public virtual ICollection<ConfiguredReporting> ConfiguredReportings { get; set; }
    public virtual ICollection<DeviceFilter> DeviceFilters { get; set; }
    public virtual ICollection<Option> Options { get; set; }
}