using System;
using System.Reflection;
using System.Linq;

// Load the assembly
var asm = typeof(ModelContextProtocol.Server.McpServer).Assembly;

// Look for McpRequestFilter
var types = asm.GetTypes().Where(t => t.Name.Contains("RequestFilter") || t.Name.Contains("McpRequestFilter")).ToArray();
foreach (var t in types)
{
    Console.WriteLine($"Type: {t.FullName}");
    Console.WriteLine($"  IsDelegate: {typeof(Delegate).IsAssignableFrom(t)}");
    if (typeof(Delegate).IsAssignableFrom(t))
    {
        var invoke = t.GetMethod("Invoke");
        if (invoke != null)
        {
            Console.WriteLine($"  Signature: {invoke.ReturnType.Name} ({string.Join(", ", invoke.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
        }
    }
    Console.WriteLine();
}

// Also look for RequestContext
var contextTypes = asm.GetTypes().Where(t => t.Name.Contains("RequestContext")).ToArray();
foreach (var t in contextTypes)
{
    Console.WriteLine($"Context: {t.FullName}");
    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        Console.WriteLine($"  Property: {p.PropertyType.Name} {p.Name}");
    }
}
