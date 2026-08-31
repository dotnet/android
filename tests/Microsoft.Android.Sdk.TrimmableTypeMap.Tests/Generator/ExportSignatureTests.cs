using System.IO;
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
		Assert.Equal ("(Ljava/io/InputStream;)V", mappedMethod.JniSignature);
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
}
