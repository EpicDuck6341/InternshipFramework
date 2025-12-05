using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Elijah.Domain.Entities.Abstract;

// ------------------------------------------------------------ //
// Entity storing Zigbee reporting configuration entries        //
// Maps to database table for persistent device reporting rules //
// ------------------------------------------------------------ //

namespace Elijah.Domain.Entities;

[Table("ConfiguredReporting")]
public class ConfiguredReporting : BaseType
{
    [Key]
    public int Id { get; set; }

    // Zigbee cluster identifier
    public string Cluster { get; set; }

    // Attribute within the cluster
    public string Attribute { get; set; }

    // Maximum interval between forced reports (seconds)
    public string MaximumReportInterval { get; set; }

    // Minimum interval between reports (seconds)
    public string MinimumReportInterval { get; set; }

   
    // Minimum change threshold to trigger a report 
    public string ReportableChange { get; set; }

    // Zigbee endpoint number
    public string Endpoint { get; set; }

    // Flag indicating if configuration has been modified
    public bool Changed { get; set; }

    // Flag indicating if this is a template (copied to new devices)
    public bool IsTemplate { get; set; }

    // Foreign key linking to Device entity
    public int DeviceId { get; set; }

    // Navigation property to parent Device
    [ForeignKey(nameof(DeviceId))]
    public Device Device { get; set; }
}