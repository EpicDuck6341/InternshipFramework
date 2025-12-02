using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Elijah.Domain.Entities.Abstract;

namespace Elijah.Domain.Entities;

[Table("OpenTherm")]
public class OpenTherm : BaseType //bump
{
    [Key] 
    public int Id { get; set; }
    public float Threshold {get; set;}
    public int IntervalSec { get; set; }
}