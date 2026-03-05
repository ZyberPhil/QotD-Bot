using System.ComponentModel.DataAnnotations;

namespace QotD.Bot.Data.Models;

/// <summary>
/// Represents a scheduled "Question of the Day" entry in the database.
/// </summary>
public sealed class Question
{
    /// <summary>Auto-incremented primary key.</summary>
    public int Id { get; set; }

    /// <summary>The question text that will be posted to Discord.</summary>
    [Required]
    [MaxLength(2000)]
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>The calendar date on which this question should be posted.</summary>
    public DateOnly ScheduledFor { get; set; }

    /// <summary>Indicates whether the question has already been posted.</summary>
    public bool Posted { get; set; } = false;

    /// <summary>UTC timestamp of when this question was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
