using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public sealed class UserBirthday
{
    [Key]
    public int Id { get; set; }
    public ulong MemberId { get; set; }
    public ulong GuildId { get; set; }
    public int Day { get; set; }
    public int Month { get; set; }
}
