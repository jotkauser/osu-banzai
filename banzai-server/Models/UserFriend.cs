using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace banzai_server.Models;

[PrimaryKey("UserId", "FriendId")]
[Table("user_friends")]
public partial class UserFriend
{
    [Key]
    [Column("user_id")]
    public long UserId { get; set; }

    [Key]
    [Column("friend_id")]
    public long FriendId { get; set; }

    [Column("created_at", TypeName = "timestamp(0) without time zone")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "timestamp(0) without time zone")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("FriendId")]
    [InverseProperty("UserFriendFriends")]
    public virtual User Friend { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserFriendUsers")]
    public virtual User User { get; set; } = null!;
}
