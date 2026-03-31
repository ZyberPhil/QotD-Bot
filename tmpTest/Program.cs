using System;
using System.Linq;
using System.Reflection;
using DSharpPlus.Entities;

namespace tmpTest
{
    class Program
    {
        static void Main()
        {
            var asm = typeof(DiscordUser).Assembly;
            var targetType = typeof(System.Collections.Generic.IEnumerable<DiscordSelectDefaultValue>);
            var typesWithCtors = asm.GetTypes()
                .SelectMany(t => t.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                .Where(c => c.GetParameters().Any(p => targetType.IsAssignableFrom(p.ParameterType)))
                .Select(c => c.DeclaringType.Name)
                .Distinct()
                .ToList();
            
            Console.WriteLine("Types with constructors taking IEnumerable<DiscordSelectDefaultValue>: " + string.Join(", ", typesWithCtors));
        }
    }
}
