using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

[Table("cache")]
[Index("Expiration", Name = "cache_expiration_index")]
public partial class Cache
{
    [Key]
    [Column("key")]
    [StringLength(255)]
    public string Key { get; set; } = null!;

    [Column("value")]
    public string Value { get; set; } = null!;

    [Column("expiration")]
    public long Expiration { get; set; }
}
