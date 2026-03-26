namespace QotD.Bot.Features.Economy.Models;

public class EconomyResult 
{
    public bool IsApiAvailable { get; set; } = true;
    public bool IsSuccess { get; set; }
    public long? Balance { get; set; }
    public string? ErrorMessage { get; set; }

    public static EconomyResult Unavailable() => new() 
    { 
        IsApiAvailable = false, 
        IsSuccess = false, 
        ErrorMessage = "Economy API ist derzeit nicht erreichbar." 
    };

    public static EconomyResult Success(long? balance = null) => new() 
    { 
        IsApiAvailable = true, 
        IsSuccess = true, 
        Balance = balance 
    };

    public static EconomyResult Failure(string error) => new() 
    { 
        IsApiAvailable = true, 
        IsSuccess = false, 
        ErrorMessage = error 
    };
}
