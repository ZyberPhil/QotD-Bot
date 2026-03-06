using System;
using System.Reflection;
using System.Linq;

Assembly asm = Assembly.LoadFrom("/home/phil/.nuget/packages/dsharpplus/5.0.0-nightly-02574/lib/net9.0/DSharpPlus.dll");
var types = asm.GetTypes().Select(t => t.Name).Where(n => n.Contains("Builder") || n.Contains("Host") || n.Contains("Client")).ToList();
foreach(var t in types) { Console.WriteLine(t); }
