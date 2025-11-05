using System.ComponentModel.DataAnnotations.Schema;

namespace Elijah.Domain.Entities.Abstract;

/// <summary>
/// The base type to be used for all tables across the databases.
/// </summary>
public abstract class BaseType
{
    [Column(Order = 1)]
    public DateTimeOffset SysCreated { get; set; }

    [Column(Order = 2)]
    public DateTimeOffset? SysModified { get; set; }

    [Column(Order = 3)]
    public bool SysRemoved { get; set; }
}
