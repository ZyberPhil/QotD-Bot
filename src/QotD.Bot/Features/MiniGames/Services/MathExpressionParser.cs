using System.Data;

namespace QotD.Bot.Features.MiniGames.Services;

public static class MathExpressionParser
{
    private static readonly DataTable _dataTable = new();

    public static double? TryEvaluate(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression)) return null;

        expression = expression.Replace(" ", "");

        try
        {
            // Handle Factorial
            if (expression.EndsWith('!'))
            {
                var numStr = expression[..^1];
                if (int.TryParse(numStr, out int num) && num >= 0 && num <= 20)
                {
                    return Factorial(num);
                }
                return null;
            }

            // Handle Sqrt
            if (expression.StartsWith("sqrt(") && expression.EndsWith(')'))
            {
                var numStr = expression.Substring(5, expression.Length - 6);
                var innerResult = TryEvaluate(numStr);
                if (innerResult.HasValue && innerResult.Value >= 0)
                {
                    return Math.Sqrt(innerResult.Value);
                }
                return null;
            }

            // Handle Power
            if (expression.Contains('^'))
            {
                var parts = expression.Split('^');
                if (parts.Length == 2)
                {
                    var baseResult = TryEvaluate(parts[0]);
                    var expResult = TryEvaluate(parts[1]);
                    if (baseResult.HasValue && expResult.HasValue)
                    {
                        return Math.Pow(baseResult.Value, expResult.Value);
                    }
                }
                return null;
            }
            
            // Standard Math via DataTable
            var result = _dataTable.Compute(expression, "");
            return Convert.ToDouble(result);
        }
        catch
        {
            return null;
        }
    }

    private static long Factorial(int n)
    {
        if (n < 0) throw new ArgumentException(nameof(n));
        if (n <= 1) return 1;
        long result = 1;
        for (int i = 2; i <= n; i++) result *= i;
        return result;
    }
}
