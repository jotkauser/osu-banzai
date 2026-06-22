using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

[Table("cache_locks")]
[Index("Expiration", Name = "cache_locks_expiration_index")]
public partial class CacheLock
{
    [Key]
    [Column("key")]
    [StringLength(255)]
    public string Key { get; set; } = null!;

    [Column("owner")]
    [StringLength(255)]
    public string Owner { get; set; } = null!;

    [Column("expiration")]
    public long Expiration { get; set; }
}
