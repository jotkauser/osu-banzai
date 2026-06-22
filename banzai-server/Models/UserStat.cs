using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

[PrimaryKey("UserId", "Mode")]
[Table("user_stats")]
public partial class UserStat
{
    [Key]
    [Column("user_id")]
    public long UserId { get; set; }

    [Key]
    [Column("mode")]
    public short Mode { get; set; }

    [Column("ranked_score")]
    public long RankedScore { get; set; }

    [Column("total_score")]
    public long TotalScore { get; set; }

    [Column("pp")]
    public int Pp { get; set; }

    [Column("playcount")]
    public int Playcount { get; set; }

    [Column("accuracy")]
    public double Accuracy { get; set; }

    [Column("max_combo")]
    public int MaxCombo { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("UserStats")]
    public virtual User User { get; set; } = null!;
}
