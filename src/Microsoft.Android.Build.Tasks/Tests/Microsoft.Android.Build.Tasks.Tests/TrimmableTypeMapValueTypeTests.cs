using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

using Microsoft.Android.Sdk.TrimmableTypeMap;

using NUnit.Framework;

namespace Xamarin.Android.Build.Tests;

[TestFixture]
public class TrimmableTypeMapValueTypeTests
{
	// Kotlin value-class bindings are structs with [Register], but they are not Java peers.
	[Test]
	public void RegisteredValueTypeIsNotAJavaPeer ()
	{
		using var assembly = CreateRegisteredValueTypeAssembly ();
		using var reader = new PEReader (assembly);
		using var scanner = new JavaPeerScanner ();

		Assert.IsEmpty (scanner.Scan ([
			new AssemblyInput ("ValueClasses", "/ValueClasses.dll", reader),
		]));
	}

	static MemoryStream CreateRegisteredValueTypeAssembly ()
	{
		var pe = new PEAssemblyBuilder (new Version (11, 0, 0, 0));
		pe.EmitPreamble ("ValueClasses", "ValueClasses.dll");

		var valueTypeRef = pe.Metadata.AddTypeReference (
			pe.SystemRuntimeRef,
			pe.Metadata.GetOrAddString ("System"),
			pe.Metadata.GetOrAddString ("ValueType"));
		var dp = pe.Metadata.AddTypeDefinition (
			TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
			pe.Metadata.GetOrAddString ("Test"),
			pe.Metadata.GetOrAddString ("MyDp"),
			valueTypeRef,
			MetadataTokens.FieldDefinitionHandle (pe.Metadata.GetRowCount (TableIndex.Field) + 1),
			MetadataTokens.MethodDefinitionHandle (pe.Metadata.GetRowCount (TableIndex.MethodDef) + 1));

		var registerAttribute = pe.Metadata.AddTypeReference (
			pe.MonoAndroidRef,
			pe.Metadata.GetOrAddString ("Android.Runtime"),
			pe.Metadata.GetOrAddString ("RegisterAttribute"));
		var registerCtor = pe.AddMemberRef (registerAttribute, ".ctor",
			sig => sig.MethodSignature (isInstanceMethod: true).Parameters (1,
				rt => rt.Void (),
				p => p.AddParameter ().Type ().String ()));
		pe.Metadata.AddCustomAttribute (
			dp,
			registerCtor,
			pe.BuildAttributeBlob (blob => blob.WriteSerializedString ("com/example/MyDp")));

		var stream = new MemoryStream ();
		pe.WritePE (stream);
		stream.Position = 0;
		return stream;
	}
}
