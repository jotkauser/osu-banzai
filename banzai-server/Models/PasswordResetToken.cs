using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

[Table("password_reset_tokens")]
public partial class PasswordResetToken
{
    [Key]
    [Column("email")]
    [StringLength(255)]
    public string Email { get; set; } = null!;

    [Column("token")]
    [StringLength(255)]
    public string Token { get; set; } = null!;

    [Column("created_at", TypeName = "timestamp(0) without time zone")]
    public DateTime? CreatedAt { get; set; }
}
