
using System;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.Metadata;

class Program {
    static void Main() {
        Console.WriteLine("MethodBody Methods:");
        foreach(var m in typeof(MethodBody).GetMethods()) {
            Console.WriteLine(m);
        }
    }
}

