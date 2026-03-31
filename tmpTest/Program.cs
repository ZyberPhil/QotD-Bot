using System;
using System.Linq;
using DSharpPlus.Entities;

namespace tmpTest
{
    class Program
    {
        static void Main()
        {
            var t = typeof(DiscordMessage);
            var ms = t.GetMethods().Where(x => x.Name == "ModifyAsync");
            Console.WriteLine("ModifyAsync Overloads:");
            foreach (var m in ms)
            {
                Console.WriteLine($"- {m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
            }
        }
    }
}
