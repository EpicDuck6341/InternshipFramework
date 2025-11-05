using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Elijah.Domain.Entities;

public class ConfiguredReporting
{
    [Key]
    public int Id { get; set; }
    public string Address { get; set; } // Foreign Key
    public string Cluster { get; set; }
    public string Attribute { get; set; }
    public string MaximumReportInterval { get; set; }
    public string MinimumReportInterval { get; set; }
    public string ReportableChange { get; set; }
    public string Endpoint { get; set; }
    public bool Changed { get; set; }

    [ForeignKey(nameof(Address))]
    public virtual Device Device { get; set; }
}