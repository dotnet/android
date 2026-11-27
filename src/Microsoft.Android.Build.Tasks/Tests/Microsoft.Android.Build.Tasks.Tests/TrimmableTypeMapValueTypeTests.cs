using System;
using System.IO;
using System.Linq;
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
	// A Kotlin value-class struct gets only a typemap entry, trimmed with the struct,
	// so the typemap-based R8 rules keep the Kotlin class only when the struct is used.
	[Test]
	public void RegisteredValueTypeGetsOnlyATypeMapEntry ()
	{
		using var assembly = CreateRegisteredValueTypeAssembly ();
		using var reader = new PEReader (assembly);
		using var scanner = new JavaPeerScanner ();

		var peers = scanner.Scan ([
			new AssemblyInput ("ValueClasses", "/ValueClasses.dll", reader),
		]);
		var peer = peers.Single ();
		Assert.IsTrue (peer.IsKotlinValueClass);

		var model = ModelBuilder.Build (peers, "/_ValueClasses.TypeMap.dll");
		Assert.IsEmpty (model.ProxyTypes);
		Assert.IsEmpty (model.Associations);
		var entry = model.Entries.Single ();
		Assert.AreEqual ("com/example/MyDp", entry.MapKey);
		Assert.AreEqual ("Test.MyDp, ValueClasses", entry.ProxyTypeReference);
		Assert.AreEqual ("Test.MyDp, ValueClasses", entry.TargetTypeReference);
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
