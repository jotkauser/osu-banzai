using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace banzai_server.Models;

[Table("chat_messages")]
public class ChatMessage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("from_id")]
    public long FromId { get; set; }

    [Column("channel_id")]
    public long? ChannelId { get; set; }

    [Column("message")]
    [StringLength(2048)]
    public string Message { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("FromId")]
    public User? From { get; set; }

    [ForeignKey("ChannelId")]
    public ChatChannel? Channel { get; set; }
}
