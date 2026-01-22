using System;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.IO;

var dllPath = "samples/HelloWorld/HelloWorld/obj/Release/net11.0-android/android-arm64/linked/Mono.Android.dll";

if (!File.Exists(dllPath))
{
    Console.WriteLine($"ERROR: File not found: {dllPath}");
    return 1;
}

try
{
    using (var stream = new FileStream(dllPath, FileMode.Open, FileAccess.Read))
    using (var peReader = new PEReader(stream))
    {
        var metadataReader = peReader.GetMetadataReader();
        
        // Get the assembly definition
        var assemblyDef = metadataReader.GetAssemblyDefinition();
        var customAttrs = assemblyDef.GetCustomAttributes();
        
        bool found = false;
        foreach (var attrHandle in customAttrs)
        {
            var attr = metadataReader.GetCustomAttribute(attrHandle);
            var attrType = attr.Constructor.Kind == HandleKind.MethodDefinition
                ? metadataReader.GetMethodDefinition((MethodDefinitionHandle)attr.Constructor).GetDeclaringType()
                : metadataReader.GetMemberReference((MemberReferenceHandle)attr.Constructor).Parent;
            
            var typeDefOrRef = attrType;
            string typeName = GetTypeName(metadataReader, typeDefOrRef);
            
            Console.WriteLine($"Attribute: {typeName}");
            
            if (typeName.Contains("TypeMapAssemblyTargetAttribute"))
            {
                found = true;
            }
        }
        
        if (found)
        {
            Console.WriteLine("\n✓ SUCCESS: 'System.Runtime.InteropServices.TypeMapAssemblyTargetAttribute' found in assembly!");
            return 0;
        }
        else
        {
            Console.WriteLine("\n✗ NOT FOUND: 'System.Runtime.InteropServices.TypeMapAssemblyTargetAttribute' not found in assembly");
            return 1;
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
    return 1;
}

string GetTypeName(MetadataReader reader, EntityHandle handle)
{
    return handle.Kind switch
    {
        HandleKind.TypeDefinition => GetTypeDefName(reader, (TypeDefinitionHandle)handle),
        HandleKind.TypeReference => GetTypeRefName(reader, (TypeReferenceHandle)handle),
        _ => "Unknown"
    };
}

string GetTypeDefName(MetadataReader reader, TypeDefinitionHandle handle)
{
    var typeDef = reader.GetTypeDefinition(handle);
    var ns = reader.GetString(typeDef.Namespace);
    var name = reader.GetString(typeDef.Name);
    return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
}

string GetTypeRefName(MetadataReader reader, TypeReferenceHandle handle)
{
    var typeRef = reader.GetTypeReference(handle);
    var ns = reader.GetString(typeRef.Namespace);
    var name = reader.GetString(typeRef.Name);
    return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
}
