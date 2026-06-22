using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

[Table("job_batches")]
public partial class JobBatch
{
    [Key]
    [Column("id")]
    [StringLength(255)]
    public string Id { get; set; } = null!;

    [Column("name")]
    [StringLength(255)]
    public string Name { get; set; } = null!;

    [Column("total_jobs")]
    public int TotalJobs { get; set; }

    [Column("pending_jobs")]
    public int PendingJobs { get; set; }

    [Column("failed_jobs")]
    public int FailedJobs { get; set; }

    [Column("failed_job_ids")]
    public string FailedJobIds { get; set; } = null!;

    [Column("options")]
    public string? Options { get; set; }

    [Column("cancelled_at")]
    public int? CancelledAt { get; set; }

    [Column("created_at")]
    public int CreatedAt { get; set; }

    [Column("finished_at")]
    public int? FinishedAt { get; set; }
}
