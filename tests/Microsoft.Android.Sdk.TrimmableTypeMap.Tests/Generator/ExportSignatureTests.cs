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
}
