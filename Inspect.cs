using System;
using System.Linq;
using System.Reflection;

class Program {
    static void Main(string[] args) {
        try {
            var path = args[0];
            Console.WriteLine($"Loading {path}");
            var asm = Assembly.LoadFrom(path);
            var typeName = "Mono.Android._.Java.Interop.TypeManager_JavaTypeManager_JavaTypeManager_Proxy";
            var type = asm.GetType(typeName);
            if (type == null) {
                Console.WriteLine($"Type {typeName} not found by GetType. Searching exported types...");
                type = asm.GetTypes().FirstOrDefault(t => t.FullName == typeName);
            }
            
            if (type == null) {
                Console.WriteLine("Type not found in assembly.");
                return;
            }
            Console.WriteLine($"Type: {type.FullName}");
            Console.WriteLine("Methods:");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)) {
                Console.WriteLine($" - {method.Name} (Virtual: {method.IsVirtual}, Abstract: {method.IsAbstract})");
            }
        } catch (Exception ex) {
            Console.WriteLine($"Error: {ex}");
            if (ex is ReflectionTypeLoadException rtle) {
                foreach (var le in rtle.LoaderExceptions) {
                    Console.WriteLine($"LoaderException: {le}");
                }
            }
        }
    }
}