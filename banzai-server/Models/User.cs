using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

[Table("users")]
[Index("Email", Name = "users_email_unique", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    [StringLength(255)]
    public string Name { get; set; } = null!;

    [Column("email")]
    [StringLength(255)]
    public string Email { get; set; } = null!;

    [Column("email_verified_at", TypeName = "timestamp(0) without time zone")]
    public DateTime? EmailVerifiedAt { get; set; }

    [Column("password")]
    [StringLength(255)]
    public string Password { get; set; } = null!;

    [Column("country")]
    [StringLength(2)]
    public string Country { get; set; } = null!;

    [Column("privileges")]
    public int Privileges { get; set; }

    [Column("remember_token")]
    [StringLength(100)]
    public string? RememberToken { get; set; }

    [Column("created_at", TypeName = "timestamp(0) without time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp(0) without time zone")]
    public DateTime? UpdatedAt { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<UserStat> UserStats { get; set; } = new List<UserStat>();
}
