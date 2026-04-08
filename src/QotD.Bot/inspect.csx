using System;
using System.Reflection;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

var types = new[] { 
    typeof(DiscordInteractionResponseBuilder), 
    typeof(ModalSubmittedEventArgs),
    typeof(IModalSubmission),
    typeof(DiscordComponent),
    typeof(DiscordTextInputComponent)
};

foreach (var type in types)
{
    Console.WriteLine($"\n--- {type.FullName} ---");
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
    {
        Console.WriteLine($"Method: {method.Name}");
    }
    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
    {
        Console.WriteLine($"Property: {property.Name}");
    }
}
