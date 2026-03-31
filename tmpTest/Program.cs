using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DSharpPlus.Entities;

namespace tmpTest
{
    class Program
    {
        static void Main()
        {
            var roleSelect = new DiscordRoleSelectComponent("test", "test");
            var prop = roleSelect.GetType().GetProperty("DefaultValues");
            var val = prop?.GetValue(roleSelect);
            if (val != null)
            {
                Console.WriteLine("DefaultValues implementation type: " + val.GetType().FullName);
                if (val is List<DiscordSelectDefaultValue>)
                {
                    Console.WriteLine("It is a List!");
                }
                else if (val is IList<DiscordSelectDefaultValue>)
                {
                    Console.WriteLine("It is an IList!");
                }
            }
            else
            {
                Console.WriteLine("DefaultValues is null by default.");
            }
        }
    }
}
