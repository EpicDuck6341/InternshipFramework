using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Elijah.Domain.Entities.Abstract;

namespace Elijah.Domain.Entities;

[Table("Device", Schema = "dbo")]
public class Device : BaseType
{
    [Key] public int Id { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// Unique value for a sensor
    /// </summary>
    public string Address { get; set; }

    /// <summary>
    /// Subscribed to get sensordata
    /// </summary>
    public bool Subscribed { get; set; }

    public int TemplateId { get; set; } //Active for removed
    [ForeignKey(nameof(TemplateId))] 
    public DeviceTemplate DeviceTemplate { get; set; }

    public ICollection<ConfiguredReporting> ConfiguredReportings { get; set; }
    public ICollection<DeviceFilter> DeviceFilters { get; set; }
    public ICollection<Option> Options { get; set; }
}