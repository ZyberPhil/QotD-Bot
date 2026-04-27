namespace QotD.Bot.Data.Models;

public enum TicketStatus
{
    Open = 0,
    Claimed = 1,
    WaitingUser = 2,
    WaitingStaff = 3,
    Resolved = 4,
    Closed = 5
}

public enum TicketPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum TicketType
{
    Support = 0,
    Report = 1,
    Bewerbung = 2
}

public enum TicketLogEventType
{
    Created = 0,
    Claimed = 1,
    Closed = 2,
    Reopened = 3,
    Escalated = 4
}
