using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

[Table("direct_messages")]
public partial class DirectMessage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("from_id")]
    public long FromId { get; set; }

    [Column("to_id")]
    public long ToId { get; set; }

    [Column("message")]
    [StringLength(2048)]
    public string Message { get; set; } = null!;

    [Column("created_at", TypeName = "timestamp(0) without time zone")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("FromId")]
    [InverseProperty("DirectMessageFroms")]
    public virtual User From { get; set; } = null!;

    [ForeignKey("ToId")]
    [InverseProperty("DirectMessageTos")]
    public virtual User To { get; set; } = null!;
}
