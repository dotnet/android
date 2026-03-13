using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Tests that [Export] methods use the C# visibility in the JCW Java file.
/// </summary>
public class ExportAccessModifierTests : FixtureTestBase
{
	static string GenerateToString (JavaPeerInfo type)
	{
		var generator = new JcwJavaSourceGenerator ();
		using var writer = new StringWriter ();
		generator.Generate (type, writer);
		return writer.ToString ();
	}

	[Fact]
	public void Scanner_ExportMethod_HasIsExportTrue ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportAccessTest");
		var publicMethod = peer.MarshalMethods.First (m => m.JniName == "publicMethod");
		Assert.True (publicMethod.IsExport);
	}

	[Fact]
	public void Scanner_ExportMethod_HasCorrectJavaAccess ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportAccessTest");
		var publicMethod = peer.MarshalMethods.First (m => m.JniName == "publicMethod");
		var protectedMethod = peer.MarshalMethods.First (m => m.JniName == "protectedMethod");
		Assert.Equal ("public", publicMethod.JavaAccess);
		Assert.Equal ("protected", protectedMethod.JavaAccess);
	}

	[Fact]
	public void Scanner_RegisterMethod_IsExportFalse ()
	{
		var peer = FindFixtureByJavaName ("my/app/MixedMethods");
		var customMethod = peer.MarshalMethods.First (m => m.JniName == "customMethod");
		Assert.False (customMethod.IsExport);
		Assert.Null (customMethod.JavaAccess);
	}

	[Fact]
	public void JcwGenerator_ProtectedExport_UsesProtectedAccess ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportAccessTest");
		var java = GenerateToString (peer);
		Assert.Contains ("protected void protectedMethod ()", java);
	}

	[Fact]
	public void JcwGenerator_PublicExport_UsesPublicAccess ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportAccessTest");
		var java = GenerateToString (peer);
		Assert.Contains ("public void publicMethod ()", java);
	}
}
