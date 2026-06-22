using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

[Table("chat_messages")]
public partial class ChatMessage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("from_id")]
    public long FromId { get; set; }

    [Column("message")]
    [StringLength(2048)]
    public string Message { get; set; } = null!;

    [Column("created_at", TypeName = "timestamp(0) without time zone")]
    public DateTime CreatedAt { get; set; }

    [Column("channel_id")]
    public long? ChannelId { get; set; }

    [ForeignKey("ChannelId")]
    [InverseProperty("ChatMessages")]
    public virtual ChatChannel? Channel { get; set; }

    [ForeignKey("FromId")]
    [InverseProperty("ChatMessages")]
    public virtual User From { get; set; } = null!;
}
