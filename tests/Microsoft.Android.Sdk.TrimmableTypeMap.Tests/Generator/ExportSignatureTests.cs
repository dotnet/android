using System;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class ExportSignatureTests : FixtureTestBase
{
	[Theory]
	[InlineData ("my/app/ExportWithUnsupportedManagedParameter")]
	[InlineData ("my/app/ExportWithUnsupportedManagedReturn")]
	[InlineData ("my/app/ExportFieldWithUnsupportedManagedReturn")]
	[InlineData ("my/app/ExportWithGenericMethodParameter")]
	[InlineData ("my/app/ExportWithGenericInstantiation")]
	[InlineData ("my/app/ExportWithInvalidExportParameterType")]
	[InlineData ("my/app/ExportWithGenericExportParameter")]
	[InlineData ("my/app/ExportFieldWithInvalidExportParameterType")]
	[InlineData ("my/app/GenericExportType")]
	public void ScannerAndGenerator_UnsupportedExportSignatureProducesNoMember (string javaName)
	{
		var peer = FindFixtureByJavaName (javaName);

		Assert.DoesNotContain (peer.MarshalMethods, method => method.ManagedMethodName == "UnsupportedMember");
		Assert.Empty (peer.JavaFields);

		using var writer = new StringWriter ();
		new JcwJavaSourceGenerator ().Generate (peer, writer);
		Assert.DoesNotContain (" unsupported (", writer.ToString (), System.StringComparison.Ordinal);
	}

	[Fact]
	public void ScannerAndGenerator_IgnoreExportAttributeLookalikes ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportAttributeLookalikes");

		Assert.DoesNotContain (peer.MarshalMethods, method => method.ManagedMethodName == "LookalikeExport");
		Assert.Contains (peer.MarshalMethods, method =>
			method.ManagedMethodName == "RealExport" &&
			method.JniName == "realExport" &&
			method.JniSignature == "(Ljava/lang/String;)Ljava/lang/String;");

		using var writer = new StringWriter ();
		new JcwJavaSourceGenerator ().Generate (peer, writer);
		var java = writer.ToString ();
		Assert.DoesNotContain ("NOT_AN_EXPORT", java, System.StringComparison.Ordinal);
		Assert.Contains ("realExport (java.lang.String", java, System.StringComparison.Ordinal);
	}

	[Fact]
	public void Scanner_ExportConstructors_ValidateParametersAndPreserveMappings ()
	{
		foreach (var javaName in new [] {
			"my/app/ExportConstructorUnsupportedManagedParameter",
			"my/app/ExportConstructorInvalidExportParameter",
		}) {
			var invalidPeer = FindFixtureByJavaName (javaName);
			Assert.DoesNotContain (invalidPeer.MarshalMethods, method => method.IsConstructor && method.IsExport);
		}

		var mappedPeer = FindFixtureByJavaName ("my/app/ExportConstructorMappedParameter");
		var mappedMethod = Assert.Single (mappedPeer.MarshalMethods, method => method.IsConstructor && method.IsExport);
		Assert.Equal (".ctor", mappedMethod.JniName);
		Assert.Equal ("(Ljava/io/InputStream;)V", mappedMethod.JniSignature);
		Assert.DoesNotContain (mappedPeer.MarshalMethods, method => method.JniName == "notAConstructor");
		Assert.Equal ([ExportParameterKindInfo.InputStream], mappedMethod.ManagedParameterExportKinds);
		var mappedConstructor = Assert.Single (
			mappedPeer.JavaConstructors,
			constructor => constructor.JniSignature == "(Ljava/io/InputStream;)V");
		Assert.True (mappedConstructor.HasMatchingManagedCtor);
		Assert.Equal ([ExportParameterKindInfo.InputStream], mappedConstructor.ManagedParameterExportKinds);

		var staticPeer = FindFixtureByJavaName ("my/app/ExportStaticConstructor");
		Assert.DoesNotContain (staticPeer.MarshalMethods, method => method.ManagedMethodName == ".cctor");
	}

	[Theory]
	[InlineData ("!0")]
	[InlineData ("!!0")]
	[InlineData ("System.Int32&")]
	[InlineData ("System.Int32*")]
	[InlineData ("delegate*")]
	[InlineData ("System.String[,]")]
	[InlineData ("System.String[,][]")]
	[InlineData ("System.Int32*[]")]
	[InlineData ("delegate*[]")]
	public void ConstructorDiagnostics_OwnUnsupportedSignatureShapes (string managedTypeName)
	{
		Assert.True (JavaPeerScanner.IsOwnedByConstructorDiagnostics (new TypeRefData {
			ManagedTypeName = managedTypeName,
			AssemblyName = "Test",
		}));
	}

	[Fact]
	public void ConstructorDiagnostics_OwnGenericInstantiationsOnly ()
	{
		Assert.True (JavaPeerScanner.IsOwnedByConstructorDiagnostics (new TypeRefData {
			ManagedTypeName = "System.Collections.Generic.List`1",
			AssemblyName = "System.Collections",
			GenericArguments = [
				new TypeRefData {
					ManagedTypeName = "System.String",
					AssemblyName = "System.Runtime",
				},
			],
		}));
		Assert.False (JavaPeerScanner.IsOwnedByConstructorDiagnostics (new TypeRefData {
			ManagedTypeName = "MyApp.ManagedOnly",
			AssemblyName = "Test",
		}));
		Assert.False (JavaPeerScanner.IsOwnedByConstructorDiagnostics (new TypeRefData {
			ManagedTypeName = "System.String[]",
			AssemblyName = "System.Runtime",
		}));
	}

	[Theory]
	[InlineData ("System.Int32&")]
	[InlineData ("System.Int32*")]
	[InlineData ("System.String[,]")]
	public void ExportMethods_DoNotMapConstructorOnlySignatureShapes (string managedTypeName)
	{
		using var scanner = new JavaPeerScanner ();

		Assert.False (scanner.HasExportSignatureMapping (new TypeRefData {
			ManagedTypeName = managedTypeName,
			AssemblyName = "System.Runtime",
		}, ExportParameterKindInfo.Unspecified));
	}

	[Theory]
	[InlineData ("System.Void", "System.Runtime")]
	[InlineData ("System.Boolean", "System.Runtime")]
	[InlineData ("System.Byte", "System.Runtime")]
	[InlineData ("System.SByte", "System.Runtime")]
	[InlineData ("System.Char", "System.Runtime")]
	[InlineData ("System.Int16", "System.Runtime")]
	[InlineData ("System.UInt16", "System.Runtime")]
	[InlineData ("System.Int32", "System.Runtime")]
	[InlineData ("System.UInt32", "System.Runtime")]
	[InlineData ("System.Int64", "System.Runtime")]
	[InlineData ("System.UInt64", "System.Runtime")]
	[InlineData ("System.Single", "System.Runtime")]
	[InlineData ("System.Double", "System.Runtime")]
	[InlineData ("System.String", "System.Runtime")]
	[InlineData ("System.String[]", "System.Runtime")]
	[InlineData ("Java.Lang.ICharSequence", "Mono.Android")]
	[InlineData ("System.Collections.IList", "System.Runtime")]
	[InlineData ("System.Collections.IDictionary", "System.Runtime")]
	[InlineData ("System.Collections.ICollection", "System.Runtime")]
	public void NameBasedMappings_RequireCanonicalAssemblyIdentity (string managedTypeName, string canonicalAssemblyName)
	{
		using var scanner = new JavaPeerScanner ();

		Assert.True (scanner.HasExportSignatureMapping (new TypeRefData {
			ManagedTypeName = managedTypeName,
			AssemblyName = canonicalAssemblyName,
		}, ExportParameterKindInfo.Unspecified));
		Assert.False (scanner.HasExportSignatureMapping (new TypeRefData {
			ManagedTypeName = managedTypeName,
			AssemblyName = "User.Types",
		}, ExportParameterKindInfo.Unspecified));
	}

	[Fact]
	public void SpecialXmlMapping_AcceptsCanonicalAndForwardedFrameworkIdentity ()
	{
		using var canonicalStream = CreateTypeAssembly ("System.Private.Xml", "System.Xml", "XmlReader");
		using var readerWriterStream = CreateTypeForwarderAssembly (
			"System.Xml.ReaderWriter",
			"System.Xml",
			"XmlReader",
			"System.Private.Xml");
		using var netstandardStream = CreateTypeForwarderAssembly (
			"netstandard",
			"System.Xml",
			"XmlReader",
			"System.Xml.ReaderWriter");
		using var userStream = CreateTypeAssembly ("User.Xml", "System.Xml", "XmlReader");
		using var canonicalReader = new PEReader (canonicalStream, PEStreamOptions.LeaveOpen);
		using var readerWriterReader = new PEReader (readerWriterStream, PEStreamOptions.LeaveOpen);
		using var netstandardReader = new PEReader (netstandardStream, PEStreamOptions.LeaveOpen);
		using var userReader = new PEReader (userStream, PEStreamOptions.LeaveOpen);
		using var scanner = new JavaPeerScanner ();
		scanner.Scan (new [] {
			("System.Private.Xml", canonicalReader),
			("System.Xml.ReaderWriter", readerWriterReader),
			("netstandard", netstandardReader),
			("User.Xml", userReader),
		});

		Assert.True (scanner.HasExportSignatureMapping (new TypeRefData {
			ManagedTypeName = "System.Xml.XmlReader",
			AssemblyName = "System.Private.Xml",
		}, ExportParameterKindInfo.XmlPullParser));
		Assert.True (scanner.HasExportSignatureMapping (new TypeRefData {
			ManagedTypeName = "System.Xml.XmlReader",
			AssemblyName = "netstandard",
		}, ExportParameterKindInfo.XmlResourceParser));
		Assert.False (scanner.HasExportSignatureMapping (new TypeRefData {
			ManagedTypeName = "System.Xml.XmlReader",
			AssemblyName = "User.Xml",
		}, ExportParameterKindInfo.XmlPullParser));
	}

	static MemoryStream CreateTypeAssembly (string assemblyName, string ns, string typeName)
	{
		var stream = new MemoryStream ();
		var pe = new PEAssemblyBuilder (new Version (11, 0, 0, 0));
		pe.EmitPreamble (assemblyName, assemblyName + ".dll");
		pe.Metadata.AddTypeDefinition (
			TypeAttributes.Public,
			pe.Metadata.GetOrAddString (ns),
			pe.Metadata.GetOrAddString (typeName),
			default,
			MetadataTokens.FieldDefinitionHandle (1),
			MetadataTokens.MethodDefinitionHandle (1));
		pe.WritePE (stream);
		stream.Position = 0;
		return stream;
	}

	static MemoryStream CreateTypeForwarderAssembly (
		string assemblyName,
		string ns,
		string typeName,
		string targetAssemblyName)
	{
		var stream = new MemoryStream ();
		var pe = new PEAssemblyBuilder (new Version (11, 0, 0, 0));
		pe.EmitPreamble (assemblyName, assemblyName + ".dll");
		var target = pe.FindOrAddAssemblyRef (targetAssemblyName);
		pe.Metadata.AddExportedType (
			(TypeAttributes) 0x00200000,
			pe.Metadata.GetOrAddString (ns),
			pe.Metadata.GetOrAddString (typeName),
			target,
			typeDefinitionId: 0);
		pe.WritePE (stream);
		stream.Position = 0;
		return stream;
	}
}
