using SkiaSharp;
// SkiaSharp.Svg 1.60.0 might be in a different namespace or library. 
// Testing if purely SkiaSharp has what we need or if we need a specific namespace.
using System.Collections.Concurrent;
using Card = QotD.Bot.Features.MiniGames.Models.Card; // Resolve ambiguity
using QotD.Bot.Features.MiniGames.Models;
using SvgLib = SkiaSharp.Extended.Svg.SKSvg; // Alias to resolve ambiguity

namespace QotD.Bot.Features.MiniGames.Services;

public class BlackjackImageService
{
    private readonly string _deckPath;
    private readonly ConcurrentDictionary<string, SKBitmap> _cardCache = new();
    private const int CardWidth = 120;
    private const int CardHeight = 180;
    private const int CardOverlap = 30; // Horizontal overlap between cards

    public BlackjackImageService()
    {
        // Path to the SVG assets
        _deckPath = Path.Combine(AppContext.BaseDirectory, "UI", "images", "blackjack_deck");
        
        // Fallback for development if AppContext.BaseDirectory doesn't contain the UI folder
        if (!Directory.Exists(_deckPath))
        {
            _deckPath = "/home/phil/Documents/QotD/QotD-Bot/src/QotD.Bot/UI/images/blackjack_deck";
        }
    }

    private SKBitmap GetCardBitmap(string fileName)
    {
        return _cardCache.GetOrAdd(fileName, _ =>
        {
            string filePath = Path.Combine(_deckPath, fileName);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Card asset not found: {filePath}");
            }

            // If SkiaSharp.Svg is indeed being used, we might need to find the correct way to load it.
            // For now, let's assume we can load it or we might need to fix the package.
            // Attempting a generic SkiaSharp approach if possible, but SVG usually needs a helper.
            
            // NOTE: If this fails again, I may need to suggest a different SVG library or 
            // check the exact class name in SkiaSharp.Svg 1.60.0.
            try 
            {
                // In older SkiaSharp.Svg, it was SkiaSharp.Extended.Svg.SKSvg
                // Let's try to just load it as a picture if the library provides it.
                // Since I can't be sure of the namespace, I'll try to find it via build errors.
                return LoadSvgWithAppropriateLibrary(filePath);
            }
            catch
            {
                // Fallback: blank card if rendering fails for some reason
                var fallback = new SKBitmap(CardWidth, CardHeight);
                using var c = new SKCanvas(fallback);
                c.Clear(SKColors.White);
                return fallback;
            }
        });
    }

    private SKBitmap LoadSvgWithAppropriateLibrary(string filePath)
    {
        var svg = new SvgLib();
        svg.Load(filePath);
        var bitmap = new SKBitmap(CardWidth, CardHeight);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(svg.Picture);
        return bitmap;
    }

    public SKBitmap CreateHandImage(List<Card> hand, bool hideFirstCard = false)
    {
        int cardCount = hand.Count;
        if (cardCount == 0) return new SKBitmap(1, 1);

        int totalWidth = CardWidth + (cardCount - 1) * CardOverlap;
        var bitmap = new SKBitmap(totalWidth, CardHeight);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        for (int i = 0; i < cardCount; i++)
        {
            string fileName = (i == 0 && hideFirstCard) ? "CardBack.svg" : hand[i].GetFileName();
            var cardBitmap = GetCardBitmap(fileName);
            canvas.DrawBitmap(cardBitmap, i * CardOverlap, 0);
        }

        return bitmap;
    }

    public byte[] CreateGameTableImage(List<Card> playerHand, List<Card> dealerHand, bool hideDealerCard)
    {
        using var playerHandBitmap = CreateHandImage(playerHand);
        using var dealerHandBitmap = CreateHandImage(dealerHand, hideDealerCard);

        int margin = 20;
        int totalWidth = Math.Max(playerHandBitmap.Width, dealerHandBitmap.Width);
        int totalHeight = playerHandBitmap.Height + dealerHandBitmap.Height + margin + 40; // 40 for labels

        var tableBitmap = new SKBitmap(totalWidth, totalHeight);
        using var canvas = new SKCanvas(tableBitmap);
        canvas.Clear(SKColors.Transparent);

        using var font = new SKFont(SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold), 24);
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };

        // Draw Dealer Hand
        canvas.DrawText("Dealer Hand:", 0, 25, SKTextAlign.Left, font, paint);
        canvas.DrawBitmap(dealerHandBitmap, 0, 35);

        // Draw Player Hand
        int playerY = dealerHandBitmap.Height + margin + 35;
        canvas.DrawText("Your Hand:", 0, playerY, SKTextAlign.Left, font, paint);
        canvas.DrawBitmap(playerHandBitmap, 0, playerY + 10);

        using var image = SKImage.FromBitmap(tableBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
