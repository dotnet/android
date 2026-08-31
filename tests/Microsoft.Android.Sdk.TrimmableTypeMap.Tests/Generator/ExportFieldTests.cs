using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Tests for the [ExportField] scanner and model contracts.
/// </summary>
public class ExportFieldTests : FixtureTestBase
{
	[Fact]
	public void Scanner_DetectsExportFieldsWithCorrectProperties ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportFieldExample");
		Assert.NotEmpty (peer.JavaFields);

		var staticField = peer.JavaFields.First (f => f.FieldName == "STATIC_INSTANCE");
		Assert.True (staticField.IsStatic);
		Assert.Equal ("GetInstance", staticField.InitializerMethodName);
		// Reference type — mapped via JNI signature to the actual Java type
		Assert.Equal ("my.app.ExportFieldExample", staticField.JavaTypeName);

		var instanceField = peer.JavaFields.First (f => f.FieldName == "VALUE");
		Assert.False (instanceField.IsStatic);
		Assert.Equal ("GetValue", instanceField.InitializerMethodName);
		Assert.Equal ("java.lang.String", instanceField.JavaTypeName);
	}

	[Fact]
	public void Scanner_ExportFieldMethod_HasExportConnectorAndFlag ()
	{
		// Gap #2 + #3: [ExportField] methods should have connector "__export__" and IsExport=true
		var peer = FindFixtureByJavaName ("my/app/ExportFieldExample");
		var getValue = peer.MarshalMethods.First (m => m.JniName == "GetValue");
		Assert.Equal ("__export__", getValue.Connector);
		Assert.True (getValue.IsExport);
		Assert.Equal ("public", getValue.JavaAccess);
	}

	[Fact]
	public void JcwGenerator_EmitsInstanceFieldDeclarationAndMethodWrapper ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportFieldExample");
		var generator = new JcwJavaSourceGenerator ();
		using var writer = new StringWriter ();
		generator.Generate (peer, writer);
		var java = writer.ToString ();

		Assert.Contains ("VALUE = GetValue ();", java);
		Assert.Contains ("GetValue ()", java);
		Assert.Contains ("n_GetValue", java);
	}

	[Theory]
	[InlineData ("my/app/ExportFieldWithParameter")]
	[InlineData ("my/app/ExportFieldWithVoidReturn")]
	[InlineData ("my/app/ExportFieldWithParameterAndVoidReturn")]
	[InlineData ("my/app/GenericExportField")]
	public void ScannerAndGenerator_InvalidExportFieldProducesNoFieldOrMethod (string javaName)
	{
		var peer = FindFixtureByJavaName (javaName);

		Assert.Empty (peer.JavaFields);
		Assert.DoesNotContain (peer.MarshalMethods, method => method.ManagedMethodName == "GetValue");

		using var writer = new StringWriter ();
		new JcwJavaSourceGenerator ().Generate (peer, writer);
		Assert.DoesNotContain ("VALUE", writer.ToString ());
	}

	[Fact]
	public void ScannerAndGenerator_IgnoreLookalikeExportFieldAttribute ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportFieldLookalike");

		Assert.Empty (peer.JavaFields);
		Assert.DoesNotContain (peer.MarshalMethods, method => method.ManagedMethodName == "GetValue");

		using var writer = new StringWriter ();
		new JcwJavaSourceGenerator ().Generate (peer, writer);
		Assert.DoesNotContain ("NOT_A_FIELD", writer.ToString ());

		var realPeer = FindFixtureByJavaName ("my/app/ExportFieldExample");
		Assert.Contains (realPeer.JavaFields, field => field.FieldName == "VALUE");
		Assert.Contains (realPeer.MarshalMethods, method => method.ManagedMethodName == "GetValue");
	}
}
