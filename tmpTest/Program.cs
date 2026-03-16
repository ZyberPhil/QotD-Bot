using System;
using System.Linq;
using DSharpPlus;
using System.Reflection;

class Program
{
    static void Main()
    {
        var asm = typeof(DiscordClient).Assembly;
        var t2 = asm.GetTypes().FirstOrDefault(t => t.Name.Contains("EventHandlingBuilder"));
        if (t2 != null)
        {
            foreach (var m in t2.GetMethods().Where(m => m.Name.Contains("AddEventHandlers"))) 
                Console.WriteLine("Method: " + m.Name);
        }
    }
}
