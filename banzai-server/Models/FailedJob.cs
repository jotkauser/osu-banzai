using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

[Table("failed_jobs")]
[Index("Connection", "Queue", "FailedAt", Name = "failed_jobs_connection_queue_failed_at_index")]
[Index("Uuid", Name = "failed_jobs_uuid_unique", IsUnique = true)]
public partial class FailedJob
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("uuid")]
    [StringLength(255)]
    public string Uuid { get; set; } = null!;

    [Column("connection")]
    [StringLength(255)]
    public string Connection { get; set; } = null!;

    [Column("queue")]
    [StringLength(255)]
    public string Queue { get; set; } = null!;

    [Column("payload")]
    public string Payload { get; set; } = null!;

    [Column("exception")]
    public string Exception { get; set; } = null!;

    [Column("failed_at", TypeName = "timestamp(0) without time zone")]
    public DateTime FailedAt { get; set; }
}
