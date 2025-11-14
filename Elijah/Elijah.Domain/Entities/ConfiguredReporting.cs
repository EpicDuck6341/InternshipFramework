using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Elijah.Domain.Entities.Abstract;

namespace Elijah.Domain.Entities;

[Table("ConfiguredReporting", Schema = "dbo")]
public class ConfiguredReporting: BaseType
{
    [Key] 
    public int Id { get; set; }

    public string Cluster { get; set; }
    public string Attribute { get; set; }
    public string MaximumReportInterval { get; set; }
    public string MinimumReportInterval { get; set; } //n Istemplate

    /// <summary>
    /// Increment wanneer er gerapporteerd moet worden e.g 0,2°C  (melden als de temperatuur 0.2 grade veranderd)
    /// </summary>
    public string ReportableChange { get; set; }

    public string Endpoint { get; set; }
    public bool Changed { get; set; }

    public bool IsTemplate { get; set; }
    
    // public string TemplateId { get; set; }
    // [ForeignKey(nameof(TemplateId))]
    // public virtual DeviceTemplate DeviceTemplate { get; set; }
    
    public int DeviceId { get; set; }
    [ForeignKey(nameof(DeviceId))]
    public Device Device { get; set; }
}