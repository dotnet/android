using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Tests that [Export] methods use the C# visibility in the JCW Java file.
/// </summary>
public class ExportAccessModifierTests : FixtureTestBase
{
	[Fact]
	public void Scanner_ExportMethods_HaveCorrectIsExportAndJavaAccess ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportAccessTest");
		var publicMethod = peer.MarshalMethods.First (m => m.JniName == "publicMethod");
		var protectedMethod = peer.MarshalMethods.First (m => m.JniName == "protectedMethod");
		Assert.True (publicMethod.IsExport);
		Assert.Equal ("public", publicMethod.JavaAccess);
		Assert.True (protectedMethod.IsExport);
		Assert.Equal ("protected", protectedMethod.JavaAccess);
	}

	[Fact]
	public void Scanner_RegisterMethod_IsNotExport ()
	{
		var peer = FindFixtureByJavaName ("my/app/MixedMethods");
		var customMethod = peer.MarshalMethods.First (m => m.JniName == "customMethod");
		Assert.False (customMethod.IsExport);
		Assert.Null (customMethod.JavaAccess);
	}

	[Fact]
	public void JcwGenerator_ExportMethods_UseCorrectAccessModifiers ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportAccessTest");
		var generator = new JcwJavaSourceGenerator ();
		using var writer = new StringWriter ();
		generator.Generate (peer, writer);
		var java = writer.ToString ();
		Assert.Contains ("protected void protectedMethod ()", java);
		Assert.Contains ("public void publicMethod ()", java);
	}
}
