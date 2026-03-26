using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QotD.Bot.Features.Economy.Models;

namespace QotD.Bot.Features.Economy.Services;

public interface IEconomyService
{
    Task<EconomyResult> GetBalanceAsync(ulong userId);
    Task<EconomyResult> AddCoinsAsync(ulong userId, int amount);
    Task<EconomyResult> RemoveCoinsAsync(ulong userId, int amount);
}

public class EconomyService : IEconomyService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EconomyService> _logger;
    private readonly bool _apiEnabled;

    public EconomyService(HttpClient httpClient, IConfiguration config, ILogger<EconomyService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        var baseUrl = config["EconomyApi:BaseUrl"];
        _apiEnabled = !string.IsNullOrWhiteSpace(baseUrl);
        
        if (_apiEnabled)
        {
            _httpClient.BaseAddress = new Uri(baseUrl!);
        }
    }

    public async Task<EconomyResult> GetBalanceAsync(ulong userId)
    {
        if (!_apiEnabled) return EconomyResult.Unavailable();

        try
        {
            var response = await _httpClient.GetAsync($"/api/economy/users/{userId}/balance");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
                return EconomyResult.Success(result?.Balance);
            }
            return EconomyResult.Failure($"API Error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Economy API is unavailable (GetBalance).");
            return EconomyResult.Unavailable();
        }
    }

    public async Task<EconomyResult> AddCoinsAsync(ulong userId, int amount)
    {
        if (!_apiEnabled) return EconomyResult.Unavailable();
        if (amount <= 0) return EconomyResult.Success();

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/economy/users/{userId}/add", new { Amount = amount });
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
                return EconomyResult.Success(result?.Balance);
            }
            return EconomyResult.Failure($"API Error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Economy API is unavailable (AddCoins).");
            return EconomyResult.Unavailable();
        }
    }

    public async Task<EconomyResult> RemoveCoinsAsync(ulong userId, int amount)
    {
        if (!_apiEnabled) return EconomyResult.Unavailable();
        if (amount <= 0) return EconomyResult.Success();

        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/economy/users/{userId}/remove", new { Amount = amount });
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<BalanceResponse>();
                return EconomyResult.Success(result?.Balance);
            }
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                return EconomyResult.Failure(error?.Message ?? "Nicht genug Coins.");
            }
            return EconomyResult.Failure($"API Error: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Economy API is unavailable (RemoveCoins).");
            return EconomyResult.Unavailable();
        }
    }

    private class BalanceResponse
    {
        public long Balance { get; set; }
    }

    private class ErrorResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}
