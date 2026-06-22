using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

[Table("jobs")]
[Index("Queue", Name = "jobs_queue_index")]
public partial class Job
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("queue")]
    [StringLength(255)]
    public string Queue { get; set; } = null!;

    [Column("payload")]
    public string Payload { get; set; } = null!;

    [Column("attempts")]
    public short Attempts { get; set; }

    [Column("reserved_at")]
    public int? ReservedAt { get; set; }

    [Column("available_at")]
    public int AvailableAt { get; set; }

    [Column("created_at")]
    public int CreatedAt { get; set; }
}
