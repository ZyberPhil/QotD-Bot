using System;
using System.Reflection;
using DSharpPlus.Entities;

var type = typeof(DiscordInteractionResponseBuilder);
Console.WriteLine($"Type: {type.FullName}");
foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
{
    Console.WriteLine($"Method: {method.Name}");
}
