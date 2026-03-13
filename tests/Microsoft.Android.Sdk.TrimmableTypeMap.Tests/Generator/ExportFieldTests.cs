using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Tests for [ExportField] support: the scanner must detect [ExportField] attributes
/// and the JCW generator must emit Java field declarations initialized by calling
/// the annotated method.
/// </summary>
public class ExportFieldTests : FixtureTestBase
{
	static string GenerateToString (JavaPeerInfo type)
	{
		var generator = new JcwJavaSourceGenerator ();
		using var writer = new StringWriter ();
		generator.Generate (type, writer);
		return writer.ToString ();
	}

	[Fact]
	public void Scanner_DetectsExportFieldMethods ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportFieldExample");
		Assert.NotEmpty (peer.JavaFields);
	}

	[Fact]
	public void Scanner_StaticField_HasCorrectProperties ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportFieldExample");
		var field = peer.JavaFields.First (f => f.FieldName == "STATIC_INSTANCE");
		Assert.True (field.IsStatic);
		Assert.Equal ("GetInstance", field.InitializerMethodName);
	}

	[Fact]
	public void Scanner_InstanceField_HasCorrectProperties ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportFieldExample");
		var field = peer.JavaFields.First (f => f.FieldName == "VALUE");
		Assert.False (field.IsStatic);
		Assert.Equal ("GetValue", field.InitializerMethodName);
	}

	[Fact]
	public void JcwGenerator_EmitsStaticFieldDeclaration ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportFieldExample");
		var java = GenerateToString (peer);
		Assert.Contains ("public static", java);
		Assert.Contains ("STATIC_INSTANCE = GetInstance ();", java);
	}

	[Fact]
	public void JcwGenerator_EmitsInstanceFieldDeclaration ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportFieldExample");
		var java = GenerateToString (peer);
		Assert.Contains ("VALUE = GetValue ();", java);
	}

	[Fact]
	public void JcwGenerator_EmitsExportFieldMethodWrapper ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportFieldExample");
		var java = GenerateToString (peer);
		// The method wrapper should also be emitted (via MarshalMethods)
		Assert.Contains ("GetValue ()", java);
		Assert.Contains ("n_GetValue", java);
	}
}
