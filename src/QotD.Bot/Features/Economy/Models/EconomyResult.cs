namespace QotD.Bot.Features.Economy.Models;

public class EconomyResult 
{
    public bool IsSuccess { get; set; }
    public long? Balance { get; set; }
    public string? ErrorMessage { get; set; }

    public static EconomyResult Unavailable() => new() 
    { 
        IsSuccess = false, 
        ErrorMessage = "Economy ist derzeit nicht verfügbar." 
    };

    public static EconomyResult Success(long? balance = null) => new() 
    { 
        IsSuccess = true, 
        Balance = balance 
    };

    public static EconomyResult Failure(string error) => new() 
    { 
        IsSuccess = false, 
        ErrorMessage = error 
    };
}
