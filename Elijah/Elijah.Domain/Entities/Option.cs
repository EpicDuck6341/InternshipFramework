using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Elijah.Domain.Entities.Abstract;

namespace Elijah.Domain.Entities;

[Table("Option")]
public class Option : BaseType
{
    [Key] 
    public int Id { get; set; }
    public string Description { get; set; }
    public string CurrentValue { get; set; }
    public string Property { get; set; }
    public bool IsProcessed { get; set; }
    
    public int DeviceId { get; set; }
    [ForeignKey(nameof(DeviceId))] 
    public Device Device { get; set; }
}