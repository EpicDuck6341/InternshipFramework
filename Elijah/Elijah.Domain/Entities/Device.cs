using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Elijah.Domain.Entities.Abstract;

// ------------------------------------------------------------ //
// Core entity representing a Zigbee device in the system      //
// Central table linking all device-related data               //
// ------------------------------------------------------------ //

namespace Elijah.Domain.Entities;

[Table("Device")]
public class Device : BaseType
{
    [Key]
    public int Id { get; set; }

    // Human-readable device name
    public string Name { get; set; }

    // ------------------------------------------------------------ //
    // Unique IEEE address (primary identifier for Zigbee devices)
    // ------------------------------------------------------------ //
    public string Address { get; set; }

    // ------------------------------------------------------------ //
    // MQTT subscription status (true = receiving sensor data)
    // ------------------------------------------------------------ //
    public bool Subscribed { get; set; }

    // Foreign key to device template
    public int DeviceTemplateId { get; set; }

    // Navigation property to template
    [ForeignKey(nameof(DeviceTemplateId))]
    public DeviceTemplate DeviceTemplate { get; set; }

    // Collection of reporting configurations for this device
    public ICollection<ConfiguredReporting> ConfiguredReportings { get; set; }

    // Collection of data filters for this device
    public ICollection<DeviceFilter> DeviceFilters { get; set; }

    // Collection of readable/writable options for this device
    public ICollection<Option> Options { get; set; }
}