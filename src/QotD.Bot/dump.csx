using System;
using System.Reflection;
using DSharpPlus;

class Program
{
    static void Main()
    {
        Type t = typeof(DiscordClient);
        foreach (var evt in t.GetEvents())
        {
            Console.WriteLine("Event: " + evt.Name);
        }
        foreach (var f in t.GetProperties())
        {
            Console.WriteLine("Property: " + f.Name);
        }
    }
}
