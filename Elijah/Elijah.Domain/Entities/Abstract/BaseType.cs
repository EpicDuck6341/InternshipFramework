using System.ComponentModel.DataAnnotations.Schema;

// ------------------------------------------------------- //
// Base entity providing system-level tracking properties  //
// All database entities inherit from this base class      //
// ------------------------------------------------------- //

namespace Elijah.Domain.Entities.Abstract;

public abstract class BaseType
{
    // Timestamp when record was created
    [Column(Order = 1)]
    public DateTimeOffset SysCreated { get; set; }

    // Timestamp when record was last modified (null if never)
    [Column(Order = 2)]
    public DateTimeOffset? SysModified { get; set; }

    // Soft-delete flag (true = logically deleted)
    [Column(Order = 3)]
    public bool SysRemoved { get; set; }
}