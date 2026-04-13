using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

public enum TeamWarningNoteType
{
    LeadComment = 0,
    UserStatement = 1,
    ResolutionNote = 2
}

public sealed class TeamWarningNote
{
    public int Id { get; set; }

    [Required]
    public int WarningId { get; set; }

    [Required]
    public ulong GuildId { get; set; }

    [Required]
    public ulong AuthorUserId { get; set; }

    public TeamWarningNoteType NoteType { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public TeamWarning? Warning { get; set; }
}