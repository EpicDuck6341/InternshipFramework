

namespace Elijah.Domain.Entities;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("DeviceTemplate", Schema = "dbo")]
public class DeviceTemplate
{
    [Key]
    public string ModelId { get; set; }

    [Required]
    public string Name { get; set; }

    public string Image { get; set; }
    public int NumberOfActive { get; set; }

    // Navigation
    public ICollection<Device> Devices { get; set; }
    public ICollection<ReportTemplate> ReportTemplates { get; set; }
}