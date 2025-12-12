using System;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.NCS;
using CSharpKOTOR.Formats.NCS.Compiler;

class Program
{
    static void Main()
    {
        // Test ternary
        var script = @"
            void main()
            {
                int a = 10;
                int b = 20;
                int max = (a > b) ? a : b;
                PrintInteger(max);
            }
        ";

        var ncs = NCSAuto.CompileNss(script, Game.K1);

        Console.WriteLine("=== C# Compiled Instructions ===");
        for (int i = 0; i < ncs.Instructions.Count; i++)
        {
            var ins = ncs.Instructions[i];
            Console.WriteLine($"{i}: {ins.InsType} [{string.Join(", ", ins.Args)}]");
        }
        Console.WriteLine("=== End Instructions ===");

        // Run with interpreter
        var interpreter = new Interpreter(ncs);
        interpreter.Run();

        var last = interpreter.ActionSnapshots[interpreter.ActionSnapshots.Count - 1];

        Console.WriteLine();
        Console.WriteLine("=== PrintInteger calls ===");
        Console.WriteLine($"PrintInteger (max): {last.ArgValues[0].Value}");
        Console.WriteLine();
        Console.WriteLine("Expected: max=20 (since 10 > 20 is false)");
    }
}

