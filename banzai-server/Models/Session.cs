using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

[Table("sessions")]
[Index("LastActivity", Name = "sessions_last_activity_index")]
[Index("UserId", Name = "sessions_user_id_index")]
public partial class Session
{
    [Key]
    [Column("id")]
    [StringLength(255)]
    public string Id { get; set; } = null!;

    [Column("user_id")]
    public long? UserId { get; set; }

    [Column("ip_address")]
    [StringLength(45)]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("payload")]
    public string Payload { get; set; } = null!;

    [Column("last_activity")]
    public int LastActivity { get; set; }
}
