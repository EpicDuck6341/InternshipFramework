using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Elijah.Domain.Entities.Abstract;

// -------------------------------------------------------------- //
// OpenTherm parameter configuration entity                       //
// Stores thresholds and intervals for heating system parameters  //
// -------------------------------------------------------------- //

namespace Elijah.Domain.Entities;

[Table("OpenTherm")]
public class OpenTherm : BaseType
{
    [Key] 
    public int Id { get; set; }

    // Threshold value for triggering updates
    public float Threshold { get; set; }

    // Reporting interval in seconds
    public int IntervalSec { get; set; }
}