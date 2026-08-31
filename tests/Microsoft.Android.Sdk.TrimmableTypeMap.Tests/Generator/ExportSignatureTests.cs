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
}
