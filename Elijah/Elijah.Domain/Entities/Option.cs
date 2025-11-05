using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Elijah.Domain.Entities;

public class Option
{
    [Key]
    public int Id { get; set; }
    public string Address { get; set; } // Foreign Key
    public string Description { get; set; }
    public string CurrentValue { get; set; }
    public string Property { get; set; }
    public bool Changed { get; set; }

    [ForeignKey(nameof(Address))]
    public virtual Device Device { get; set; }
}