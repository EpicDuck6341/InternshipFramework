using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Elijah.Domain.Entities.Abstract;

namespace Elijah.Domain.Entities;

[Table("DeviceFilter")]
public class DeviceFilter : BaseType //bump
{
    [Key]
    public int Id { get; set; }
    public string FilterValue { get; set; } //Active wordt removed
    
    public int DeviceId { get; set; }

    [ForeignKey(nameof(DeviceId))]
    public Device Device { get; set; }
}
